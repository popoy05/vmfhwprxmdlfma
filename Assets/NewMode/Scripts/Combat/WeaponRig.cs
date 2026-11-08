using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 캐릭터(Animator가 있는 오브젝트)에 붙어서 무기를 쥐여 준다.
    //  - 평소: 칼은 아래·앞으로, 총은 앞쪽 아래로 들고 다닌다
    //  - 공격 중: IK로 오른손을 "무기 방향 함수"가 가리키는 곳으로 뻗는다 → 직접 휘두르기 / 겨누기
    //    (방향은 NewModeCombat이 GeoFunctions로 매 프레임 계산)
    //  - 양손 총(산탄총·저격총)은 왼손도 총열을 받친다
    // combat이 없으면(캐릭터 꾸미기 미리보기) 들고 있는 자세만 보여 준다.
    [RequireComponent(typeof(Animator))]
    public class WeaponRig : MonoBehaviour
    {
        NewModeCombat m_Combat;
        Transform m_Root;
        float m_Scale = 1f;
        Animator m_Animator;
        Transform m_Hand, m_Shoulder, m_Spine, m_Chest, m_Hips;
        // 손 모양 (손 로컬 좌표): 손가락 방향 f, 엄지 쪽 방향 th, 손바닥 중심
        Vector3 m_FingerLocal, m_ThumbLocal, m_PalmLocal;
        bool m_HasFingers;
        float m_Twist, m_Lean, m_Crouch;
        float m_ArmLength;

        GameObject m_Weapon;
        Transform m_Tip;
        TrailRenderer m_Trail;
        WeaponCatalog.Info m_Info;
        float m_IKWeight;
        Vector3 m_Dir, m_Up;

        public Vector3 TipPosition => m_Tip ? m_Tip.position : transform.position + Vector3.up;
        public Vector3 WeaponForward => m_Dir;
        public GameObject Weapon => m_Weapon;

        public void Init(Transform root, float scale, NewModeCombat combat)
        {
            m_Root = root;
            m_Scale = scale;
            m_Combat = combat;
            m_Animator = GetComponent<Animator>();
            m_Hand = m_Animator.GetBoneTransform(HumanBodyBones.RightHand);
            m_Shoulder = m_Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            m_Spine = m_Animator.GetBoneTransform(HumanBodyBones.Spine);
            m_Chest = m_Animator.GetBoneTransform(HumanBodyBones.Chest);
            m_Hips = m_Animator.GetBoneTransform(HumanBodyBones.Hips);

            Transform middle = m_Animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform thumb = m_Animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            Transform index = m_Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            if (m_Hand && middle && (thumb || index))
            {
                Vector3 f = m_Hand.InverseTransformPoint(middle.position);
                Vector3 th = m_Hand.InverseTransformPoint(thumb ? thumb.position : index.position);
                m_PalmLocal = f * 0.55f;
                m_FingerLocal = f.normalized;
                m_ThumbLocal = Vector3.ProjectOnPlane(th, m_FingerLocal).normalized;
                m_HasFingers = m_ThumbLocal.sqrMagnitude > 0.5f;
            }
            Transform elbow = m_Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            m_ArmLength = m_Shoulder && elbow && m_Hand
                ? Vector3.Distance(m_Shoulder.position, elbow.position) + Vector3.Distance(elbow.position, m_Hand.position)
                : 0.55f * scale;
        }

        public void Equip(int index)
        {
            if (m_Weapon) Destroy(m_Weapon);
            m_Info = WeaponCatalog.All[index];
            m_Weapon = WeaponVisual.Create(index, m_Scale, out Vector3 tipLocal);

            m_Tip = new GameObject("Tip").transform;
            m_Tip.SetParent(m_Weapon.transform, false);
            m_Tip.localPosition = tipLocal;

            // 칼끝 궤적 (휘두를 때만 켜짐)
            m_Trail = m_Tip.gameObject.AddComponent<TrailRenderer>();
            m_Trail.sharedMaterial = GeoFx.LineMaterial;
            m_Trail.time = 0.16f;
            m_Trail.minVertexDistance = 0.02f * m_Scale;
            m_Trail.widthMultiplier = (m_Info.Kind == WeaponKind.Gun ? 0.08f : 0.5f) * m_Scale;
            m_Trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(m_Info.Color, Color.white, 0.6f), 0f), new GradientColorKey(m_Info.Color, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            m_Trail.colorGradient = gradient;
            m_Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Trail.emitting = false;

            m_Dir = CarryDirection();
            m_Up = m_Root.up;
            Place(true);
        }

        Vector3 CarryDirection()
        {
            Vector3 local = m_Info.Kind == WeaponKind.Gun ? new Vector3(0.1f, -0.55f, 0.83f) : new Vector3(0.25f, -0.85f, 0.45f);
            return m_Root.TransformDirection(local.normalized);
        }

        bool Active => m_Combat && m_Combat.SwordActive;
        // 궁극기 컷신 동안은 세상 시간이 거의 멈추므로, 무기·몸 움직임은 실제 시간으로
        float Dt => m_Combat && m_Combat.InCutscene ? Time.unscaledDeltaTime : Time.deltaTime;

        void OnAnimatorIK(int layerIndex)
        {
            if (!m_Root || !m_Shoulder) return;
            float target = Active ? 1f : 0f;
            m_IKWeight = Mathf.MoveTowards(m_IKWeight, target, Dt * (target > m_IKWeight ? 14f : 5f));
            if (m_IKWeight <= 0.001f) return;

            // 손 위치 = 어깨 + 무기 방향 × 팔 길이 × 뻗는 정도
            Vector3 dir = m_Root.TransformDirection((m_Combat.BladeDirLocal.normalized + Vector3.up * m_Combat.Recoil * 0.5f).normalized);
            Vector3 hand = m_Shoulder.position + dir * m_ArmLength * m_Combat.Reach;
            m_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, m_IKWeight);
            m_Animator.SetIKPosition(AvatarIKGoal.RightHand, hand);
            m_Animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, m_IKWeight * 0.6f);
            m_Animator.SetIKHintPosition(AvatarIKHint.RightElbow, m_Shoulder.position + dir * m_ArmLength * 0.4f - Vector3.up * 0.15f * m_Scale);

            // 양손 총: 왼손이 총열 아래를 받친다
            if (m_Info.TwoHanded && m_Weapon)
            {
                Vector3 support = m_Weapon.transform.position + m_Weapon.transform.forward * 0.38f * m_Scale - m_Weapon.transform.up * 0.04f * m_Scale;
                m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, m_IKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.LeftHand, support);
            }

            if (m_Combat.LookTarget.HasValue)
            {
                // 궁극기 컷신: 고개를 카메라(보는 사람) 쪽으로
                m_Animator.SetLookAtWeight(1f, 0.15f, 0.9f, 0.6f);
                m_Animator.SetLookAtPosition(m_Combat.LookTarget.Value);
            }
            else
            {
                m_Animator.SetLookAtWeight(m_IKWeight * 0.6f, 0.25f, 0.6f);
                m_Animator.SetLookAtPosition(m_Shoulder.position + dir * 3f * m_Scale);
            }
        }

        void LateUpdate()
        {
            Body();
            Place(false);
        }

        // 몸통 연출: 무기 방향으로 허리를 비틀고(twist), 내려칠 땐 앞으로 숙이고(lean), 무거운 공격은 무릎을 굽힌다(crouch).
        // 총은 쏠 때 반동으로 상체가 뒤로 젖혀진다.
        void Body()
        {
            if (!m_Combat || !m_Root) return;
            float twist = 0f, lean = 0f, crouch = 0f;
            if (Active)
            {
                Vector3 d = m_Combat.BladeDirLocal.normalized;
                if (m_Info.Kind == WeaponKind.Melee)
                {
                    twist = Mathf.Clamp(Mathf.Atan2(d.x, Mathf.Max(0.05f, d.z)) * Mathf.Rad2Deg * 0.45f, -40f, 40f);
                    lean = Mathf.Clamp(-d.y * 22f, -12f, 22f);
                }
                else
                {
                    twist = Mathf.Clamp(Mathf.Atan2(d.x, Mathf.Max(0.05f, d.z)) * Mathf.Rad2Deg * 0.3f, -30f, 30f);
                    lean = -m_Combat.Recoil * 14f;
                }
                twist += m_Combat.BodyTwist;
                lean += m_Combat.BodyLean;
                crouch = m_Combat.Crouch;
            }
            float k = 1f - Mathf.Exp(-18f * Dt);
            m_Twist = Mathf.Lerp(m_Twist, twist, k);
            m_Lean = Mathf.Lerp(m_Lean, lean, k);
            m_Crouch = Mathf.Lerp(m_Crouch, crouch, k);

            // 척추 두 마디에 나눠서 (월드 기준 축: 캐릭터 위·오른쪽)
            Quaternion half = Quaternion.AngleAxis(m_Twist * 0.5f, m_Root.up) * Quaternion.AngleAxis(m_Lean * 0.5f, m_Root.right);
            if (m_Spine) m_Spine.rotation = half * m_Spine.rotation;
            if (m_Chest) m_Chest.rotation = half * m_Chest.rotation;
            if (m_Hips && m_Crouch > 0.001f) m_Hips.position -= m_Root.up * m_Crouch * 0.1f * m_Scale;
        }

        void Place(bool snap)
        {
            if (!m_Weapon || !m_Hand || !m_Root) return;

            Vector3 wantDir, wantUp;
            if (Active)
            {
                wantDir = m_Root.TransformDirection(m_Combat.BladeDirLocal.normalized);
                wantUp = m_Info.Kind == WeaponKind.Gun ? m_Root.up : m_Root.TransformDirection(m_Combat.BladeUpLocal);
            }
            else
            {
                wantDir = CarryDirection();
                wantUp = m_Info.Kind == WeaponKind.Gun ? m_Root.up : m_Root.forward;
            }

            float speed = Active ? 40f : 10f;
            float k = snap ? 1f : 1f - Mathf.Exp(-speed * Dt);
            m_Dir = Vector3.Slerp(m_Dir, wantDir, k).normalized;
            m_Up = Vector3.Slerp(m_Up, wantUp, k);
            if (Vector3.Cross(m_Dir, m_Up).sqrMagnitude < 0.0001f) m_Up = m_Root.right;

            Quaternion weaponRot = Quaternion.LookRotation(m_Dir, m_Up);
            Vector3 grip = m_Hand.position - m_Dir * 0.03f * m_Scale;
            if (m_HasFingers)
            {
                // 손을 무기에 맞게 돌린다 (주먹으로 쥐기):
                //  칼: 엄지 쪽(th) = 칼날 방향, 손가락(f) = 날 쪽(무기의 −x)
                //  총: 엄지 쪽(th) = 위, 손가락(f) = 총구 방향 → 손잡이를 감싸 쥠
                Quaternion world = m_Info.Kind == WeaponKind.Gun
                    ? Quaternion.LookRotation(m_Dir, m_Up)
                    : Quaternion.LookRotation(weaponRot * Vector3.left, m_Dir);
                Quaternion local = Quaternion.LookRotation(m_FingerLocal, m_ThumbLocal);
                m_Hand.rotation = world * Quaternion.Inverse(local);
                // 손잡이(무기 원점)를 손바닥 한가운데에
                grip = m_Hand.TransformPoint(m_PalmLocal) + (world * Vector3.left) * 0.02f * m_Scale;
            }
            m_Weapon.transform.SetPositionAndRotation(grip, weaponRot);
            if (m_Trail) m_Trail.emitting = m_Combat && m_Combat.TrailOn;
        }

        void OnDestroy()
        {
            if (m_Weapon) Destroy(m_Weapon);
        }
    }
}
