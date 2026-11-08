using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 궁극기(C) 컷신 — 직업마다 완전히 다른 연출 (원신 "원소 폭발" 연출들을 참고)
    //  공통: 세상이 거의 멈추고(시간 ×0.04) 배경이 어두워지며 캐릭터만 원소 색 조명을 받음 · 글자는 왼쪽 아래 작은 스킬 이름뿐
    //  서리 검사  "서리 꽃"   : 등 뒤에서 옆으로 돌아 얼굴 앞에 칼을 세운 정면 클로즈업 → 화면 가장자리에 서리가 낀다
    //                           → 가로 일섬과 함께 서리가 깨져 흩어지고, 위로 빠지며 서리 폭발
    //  대지 전사  "운석 낙하" : 낮은 뒤쪽에서 하늘을 올려다보면 거대한 운석이 나타나 천천히 떨어진다
    //                           → 옆 원경으로 바뀌며 앞에 쾅 — 흙먼지·고리·섬광
    //  번개 암살자 "일섬"     : 허리 높이 옆모습, 눈가로 천천히 다가감 — 눈높이에 보라색 섬광 한 줄
    //                           → 캐릭터가 사라지고 빈 화면을 번개 일섬 다섯 줄이 가른다 → 뒤에서 다시 나타나 마무리
    //  황금 명사수 "정오의 결투" : 위아래 검은 띠, 허리춤 총에 손을 얹은 클로즈업 → 순간 뽑기(어깨 너머)
    //                           → 카메라가 금빛 총알을 나선으로 따라 날아감 → 명중 섬광 → 총을 돌려 세우고 금화
    public partial class NewModeCombat
    {
        /// <summary>테스트용: 컷신 진행 시간(초)마다 불림</summary>
        public static System.Action<float> DebugTick;

        // 컷신 공통 정보
        class Burst
        {
            public Camera Cam;
            public float Fov0;
            public Vector3 CamPos0;
            public Quaternion CamRot0;
            public Vector3 Center, Fwd, Right;
            public Color Color;
            public Theme Theme;
            public BurstUI UI;
            public Light Key, Rim;
            public float T;          // 지금까지 흐른 시간
        }

        IEnumerator Ultimate(SkillDef skill)
        {
            Theme th = Th;
            Color color = C;
            m_InCutscene = true;
            if (m_HitstopOn) { m_HitstopOn = false; Time.timeScale = m_SlowMo; }

            Camera cam = m_Player.Camera;
            m_Player.CameraOverride = true;
            m_Player.Aiming = false;

            // HUD 숨김
            CanvasGroup hud = UIAnim.Group(m_Canvas);
            hud.DOKill();
            hud.DOFade(0f, 0.15f).SetUpdate(true).SetLink(hud.gameObject);
            CanvasGroup playerHud = null;
            foreach (var cv in m_Player.GetComponentsInChildren<Canvas>(true))
                if (cv.name == "NewMode HUD") playerHud = UIAnim.Group(cv);
            if (playerHud) { playerHud.DOKill(); playerHud.DOFade(0f, 0.15f).SetUpdate(true).SetLink(playerHud.gameObject); }

            // 세상 멈춤 (캐릭터·무기·카메라·이펙트는 실제 시간으로)
            Animator anim = m_Player.Animator;
            AnimatorUpdateMode prevMode = anim ? anim.updateMode : AnimatorUpdateMode.Normal;
            if (anim) anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            float prevScale = Time.timeScale;
            Time.timeScale = 0.04f;
            PostFx.Cutscene(true, color);

            var b = new Burst
            {
                Cam = cam,
                Fov0 = cam ? cam.fieldOfView : 60f,
                CamPos0 = cam ? cam.transform.position : Vector3.zero,
                CamRot0 = cam ? cam.transform.rotation : Quaternion.identity,
                Center = transform.position,
                Color = color,
                Theme = th,
                UI = new BurstUI(this, skill, color),
            };
            b.Fwd = transform.forward; b.Fwd.y = 0f; b.Fwd.Normalize();
            b.Right = transform.right; b.Right.y = 0f; b.Right.Normalize();
            if (Physics.Linecast(Chest, b.Center + b.Fwd * 2f * S + b.Right * 0.8f * S + Vector3.up * 1.3f * S, ~(1 << 2), QueryTriggerInteraction.Ignore))
                b.Right = -b.Right; // 그쪽이 막혀 있으면 반대편에서 찍는다
            b.Key = MakeLight(Chest + b.Fwd * 1.1f * S + Vector3.up * 0.4f * S, color, 0f, 3.5f * S);
            b.Rim = MakeLight(Chest - b.Fwd * 1.2f * S + Vector3.up * 0.6f * S, Color.white, 0f, 3f * S);

            IEnumerator seq;
            switch (m_Weapon.Index)
            {
                case 0: seq = FrostBurst(b); break;
                case 1: seq = MeteorBurst(b); break;
                case 2: seq = ThunderBurst(b); break;
                default: seq = GoldBurst(b); break;
            }
            yield return seq;

            // ---- 원래대로
            if (b.Key) Destroy(b.Key.gameObject);
            if (b.Rim) Destroy(b.Rim.gameObject);
            SetBodyVisible(true);
            Time.timeScale = prevScale > 0.05f ? prevScale : 1f;
            if (anim) anim.updateMode = prevMode;
            PostFx.Cutscene(false, color);
            if (cam) cam.fieldOfView = b.Fov0;
            m_Player.CameraOverride = false;
            m_InCutscene = false;
            b.UI.Close();
            hud.DOFade(1f, 0.35f).SetDelay(0.15f).SetUpdate(true).SetLink(hud.gameObject);
            if (playerHud) playerHud.DOFade(1f, 0.35f).SetDelay(0.15f).SetUpdate(true).SetLink(playerHud.gameObject);
            BodyLean = 0f;
            BodyTwist = 0f;
            Crouch = 0f;
            LookTarget = null;

            FaceAttack(12f);
            SetFrame();
            m_Ultimate = true;
            yield return skill.Run();
            m_Ultimate = false;
        }

        // 한 프레임 진행 (길어진 프레임에도 건너뛰지 않게 최대 0.05초)
        static float Step() => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        void Shot(Burst b, Vector3 eye, Vector3 look, float fovMul, float shake = 0f)
        {
            if (!b.Cam) return;
            eye = Clamp(eye, look);
            if (shake > 0f) eye += m_Player.ShakeOffset() * shake;
            b.Cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            b.Cam.fieldOfView = b.Fov0 * fovMul;
        }

        void Lights(Burst b, float key, float rim)
        {
            if (b.Key) { b.Key.intensity = key; b.Key.transform.position = Chest + (b.Cam ? (b.Cam.transform.position - Chest).normalized : b.Fwd) * 1.1f * S + Vector3.up * 0.3f * S; }
            if (b.Rim) b.Rim.intensity = rim;
        }

        Vector3 Tip => m_Rig ? m_Rig.TipPosition : Chest + Vector3.up * 0.6f * S;

        // 둘레의 빛 알갱이가 target으로 빨려 든다 (한 프레임 몫)
        readonly List<(GameObject go, Vector3 from, float born)> m_Gather = new List<(GameObject, Vector3, float)>();
        float m_NextGather;

        void Gather(Burst b, Vector3 target, float radius, float every)
        {
            if (b.T >= m_NextGather)
            {
                m_NextGather = b.T + every;
                Vector3 from = Chest + Random.onUnitSphere * radius;
                if (from.y < b.Center.y + 0.2f * S) from.y = b.Center.y + 0.2f * S + Random.value * 0.6f * S;
                var go = Fx.Unscaled(Fx.Play(b.Theme.Charge, from, Quaternion.identity, 0.22f * S, 0.5f, false));
                if (go) m_Gather.Add((go, from, b.T));
            }
            for (int i = m_Gather.Count - 1; i >= 0; i--)
            {
                var c = m_Gather[i];
                if (!c.go) { m_Gather.RemoveAt(i); continue; }
                float u = Mathf.Clamp01((b.T - c.born) / 0.35f);
                c.go.transform.position = Vector3.Lerp(c.from, target, u * u);
                if (u >= 1f) { Destroy(c.go); m_Gather.RemoveAt(i); }
            }
        }

        void ClearGather()
        {
            foreach (var c in m_Gather) if (c.go) Destroy(c.go);
            m_Gather.Clear();
            m_NextGather = 0f;
        }

        void SetBodyVisible(bool visible)
        {
            if (m_Player.Animator)
                foreach (var r in m_Player.Animator.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            if (m_Rig && m_Rig.Weapon) m_Rig.Weapon.SetActive(visible);
        }

        // 터지는 순간 공통 (섬광 + 왜곡 + 고리 + 빛)
        void Release(Burst b, Vector3 at, float radius, float flash = 0.75f)
        {
            b.UI.Flash(flash);
            PostFx.Pulse(1f, b.Color);
            LightFlash.Spawn(at + Vector3.up * 0.8f * S, b.Color, 12f, radius * 1.5f, 0.6f, true);
            LightFlash.Spawn(Chest, Color.white, 6f, 4f * S, 0.25f, true);
            RingWave.Spawn(at + Vector3.up * 0.05f * S, radius, new Color(b.Color.r, b.Color.g, b.Color.b, 0.9f), 0.5f, true);
            RingWave.Spawn(at + Vector3.up * 0.06f * S, radius * 0.55f, new Color(1f, 1f, 1f, 0.7f), 0.3f, true);
            Fx.Unscaled(Fx.Play(b.Theme.Release, at, Facing(), 0.8f * S, 0.3f, false));
            m_Player.Shake(0.28f, 0.5f);
        }

        // ================================================================== 서리 검사: 서리 꽃
        IEnumerator FrostBurst(Burst b)
        {
            Vector3 c = b.Center, f = b.Fwd, r = b.Right, up = Vector3.up;
            Fx.Unscaled(Fx.Play(b.Theme.Circle, Feet, Facing(), b.Theme.CircleSize * S, 1.6f, false));
            Fx.Unscaled(Fx.Play("Snow AOE", c, Quaternion.identity, 1.2f * S, 1.6f, false));
            const float orbit = 0.45f, hold = 0.75f, slash = 0.2f, pull = 0.4f;
            bool frosted = false, cut = false, released = false;
            for (b.T = 0f; b.T < orbit + hold + slash + pull; b.T += Step())
            {
                float t = b.T;
                if (t < orbit)
                {
                    // 등 뒤 → 옆으로 돌아 정면으로 (120°), 칼을 옆으로 뽑아 듦
                    float u = GeoFunctions.EaseOutCubic(t / orbit);
                    float a = Mathf.Lerp(200f, 25f, u) * Mathf.Deg2Rad;
                    Vector3 dir = Mathf.Cos(a) * f + Mathf.Sin(a) * r;
                    Shot(b, c + dir * Mathf.Lerp(2.6f, 2.4f, u) * S + up * 1.2f * S, c + up * 1.05f * S, Mathf.Lerp(1f, 0.7f, u));
                    Pose(new Vector3(0.9f, 0.1f, 0.4f), Vector3.up, 1f);
                    BodyTwist = -20f * (1f - u);
                }
                else if (t < orbit + hold)
                {
                    // 얼굴 앞에 칼을 세운 정면 클로즈업 — 화면 가장자리에 서리가 낀다
                    float u = Mathf.SmoothStep(0f, 1f, (t - orbit) / hold);
                    Pose(new Vector3(0.18f, 0.95f, 0.55f), Vector3.right, 0.85f);
                    BodyTwist = 6f;
                    LookTarget = b.Cam ? b.Cam.transform.position : (Vector3?)null;
                    Vector3 eye = c + (f + r * 0.15f).normalized * Mathf.Lerp(3.2f, 2.7f, u) * S;
                    eye.y = c.y + 1.25f * S;
                    Shot(b, eye, c + up * 1.1f * S, Mathf.Lerp(0.72f, 0.62f, u), 0.3f);
                    if (!frosted) { frosted = true; b.UI.FrostEdges(b.Color); }
                    Gather(b, Tip, 1.4f * S, 0.05f);
                }
                else if (t < orbit + hold + slash)
                {
                    // 가로 일섬 — 서리가 깨진다
                    float u = (t - orbit - hold) / slash;
                    Pose(Vector3.Slerp(new Vector3(-0.9f, 0.2f, 0.4f), new Vector3(0.95f, 0f, 0.3f), GeoFunctions.EaseOutCubic(u)), Vector3.up, 1f);
                    TrailOn = true;
                    BodyTwist = Mathf.Lerp(30f, -30f, u);
                    if (!cut)
                    {
                        cut = true;
                        ClearGather();
                        b.UI.SlashLine(0f, 0f, Color.white, 0.25f);
                        b.UI.SlashLine(-4f, -30f, b.Color, 0.3f);
                        b.UI.FrostShatter();
                        PostFx.Pulse(0.8f, b.Color);
                        LightFlash.Spawn(Tip, b.Color, 10f, 5f * S, 0.4f, true);
                    }
                    Shot(b, c + f * 2f * S + r * 0.2f * S + up * 1.25f * S, c + up * 1.15f * S, 0.55f, 0.6f);
                }
                else
                {
                    // 위로 빠지며 서리 폭발을 내려다봄
                    float u = GeoFunctions.EaseOutCubic((t - orbit - hold - slash) / pull);
                    if (!released) { released = true; LookTarget = null; Release(b, Feet, 5f * S, 0.5f); Fx.Unscaled(Fx.Play("Crystals front attack", c + f * 0.8f * S, Facing(), 0.9f * S, 0.3f, false)); }
                    Shot(b, c + f * Mathf.Lerp(2f, 3.5f, u) * S + up * Mathf.Lerp(1.25f, 4.5f, u) * S, c + f * 1.5f * S, Mathf.Lerp(0.55f, 0.95f, u), 1f);
                }
                Lights(b, released ? 0f : 2f + 4f * Mathf.Clamp01(t / (orbit + hold)), released ? 0f : 3f);
                DebugTick?.Invoke(t);
                yield return null;
            }
            ClearGather();
        }

        // ================================================================== 대지 전사: 운석 낙하
        IEnumerator MeteorBurst(Burst b)
        {
            Vector3 c = b.Center, f = b.Fwd, r = b.Right, up = Vector3.up;
            Vector3 impact = c + f * 4.5f * S;
            impact.y = GeoFx.GroundY(impact, S);
            Vector3 sky = impact + up * 16f * S - f * 2f * S + r * 3f * S;

            // 거대한 운석: 울퉁불퉁한 바위 + 불꽃
            var meteor = new GameObject("Meteor");
            meteor.transform.position = sky;
            var rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.32f, 0.22f, 0.16f) };
            rockMat.EnableKeyword("_EMISSION");
            rockMat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.08f) * 1.4f);
            for (int i = 0; i < 6; i++)
            {
                var rock = GameObject.CreatePrimitive(i == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                Destroy(rock.GetComponent<Collider>());
                rock.layer = 2;
                rock.transform.SetParent(meteor.transform, false);
                rock.transform.localPosition = i == 0 ? Vector3.zero : Random.onUnitSphere * 0.7f * S;
                rock.transform.localRotation = Random.rotation;
                rock.transform.localScale = Vector3.one * (i == 0 ? 2.6f : 1.4f) * S;
                rock.GetComponent<Renderer>().sharedMaterial = rockMat;
            }
            Fx.Unscaled(Fx.Play("FX_Fireball", sky, Quaternion.identity, 1.6f * S, 2.2f, false, meteor.transform));
            var glow = MakeLight(sky, new Color(1f, 0.55f, 0.2f), 8f, 14f * S);
            glow.transform.SetParent(meteor.transform, true);
            Fx.Unscaled(Fx.Play(b.Theme.Circle, Feet, Facing(), b.Theme.CircleSize * S, 2f, false));
            Fx.Unscaled(Fx.Play("Dust loop", c, Quaternion.identity, 0.8f * S, 2f, false));

            const float look = 0.5f, fall = 0.85f, hit = 0.2f, rise = 0.35f;
            bool slammed = false;
            for (b.T = 0f; b.T < look + fall + hit + rise; b.T += Step())
            {
                float t = b.T;
                float fu = Mathf.Clamp01((t - look * 0.6f) / (look * 0.4f + fall));
                meteor.transform.position = Vector3.Lerp(sky, impact + up * 1.2f * S, fu * fu * fu);
                meteor.transform.Rotate(Vector3.right, 90f * Step(), Space.World);

                if (t < look + fall * 0.5f)
                {
                    // 낮은 뒤쪽에서 하늘을 올려다봄 — 도끼를 하늘로
                    float u = GeoFunctions.EaseOutCubic(Mathf.Clamp01(t / look));
                    Vector3 eye = c - f * Mathf.Lerp(1.8f, 2.4f, u) * S + r * 0.7f * S + up * Mathf.Lerp(1.2f, 0.4f, u) * S;
                    Vector3 target = Vector3.Lerp(c + up * 1.1f * S, meteor.transform.position, u * 0.85f);
                    Shot(b, eye, target, Mathf.Lerp(1f, 0.85f, u), 0.4f + t);
                    Pose(new Vector3(0.1f, 1f, 0.15f), Vector3.forward, 1f);
                    BodyLean = -14f;
                    Crouch = 0.5f;
                    if (Mathf.Repeat(t, 0.25f) < Step()) RingWave.Spawn(c + up * 0.05f * S, 3f * S, new Color(1f, 0.6f, 0.25f, 0.6f), 0.4f, true);
                    m_Player.Shake(0.05f + 0.1f * fu, 0.2f);
                }
                else if (!slammed)
                {
                    // 옆 원경 — 운석이 앞에 떨어지는 걸 크게
                    Vector3 eye = c + r * 6f * S - f * 1f * S + up * 2.2f * S;
                    Shot(b, eye, Vector3.Lerp(c, impact, 0.6f) + up * 1.5f * S, 0.9f, 0.6f);
                    Pose(new Vector3(0.05f, -0.7f, 0.7f), Vector3.right, 1f);
                    BodyLean = 25f;
                    Crouch = 1f;
                    if (fu >= 1f)
                    {
                        slammed = true;
                        meteor.SetActive(false);
                        Release(b, impact, 7f * S, 0.85f);
                        Fx.Unscaled(Fx.Play("Explosion", impact + up * 0.8f * S, Quaternion.identity, 1.4f * S, 0.3f, false));
                        Fx.Unscaled(Fx.Play("Ground AOE explosion", impact, Quaternion.identity, 1.2f * S, 0.3f, false));
                        Fx.Unscaled(Fx.Play("Dust ground", impact, Quaternion.identity, 1.4f * S, 0.3f, false));
                        b.UI.RadialLines(new Color(1f, 0.75f, 0.4f, 0.8f), 18);
                    }
                }
                else
                {
                    float u = GeoFunctions.EaseOutCubic(Mathf.Clamp01((t - look - fall) / (hit + rise)));
                    Shot(b, c + r * 6f * S - f * 1f * S + up * Mathf.Lerp(2.2f, 5f, u) * S, impact + up * 0.5f * S, Mathf.Lerp(0.9f, 1f, u), 1f);
                }
                Lights(b, slammed ? 0f : 4f, slammed ? 0f : 2.5f);
                DebugTick?.Invoke(t);
                yield return null;
            }
            Destroy(meteor);
        }

        // ================================================================== 번개 암살자: 일섬
        IEnumerator ThunderBurst(Burst b)
        {
            Vector3 c = b.Center, f = b.Fwd, r = b.Right, up = Vector3.up;
            Fx.Unscaled(Fx.Play(b.Theme.Aura, c + up * 0.9f * S, Quaternion.identity, b.Theme.AuraSize * S, 1.8f, false, transform));
            const float profile = 0.3f, glint = 0.1f, cuts = 0.32f, finish = 0.22f; // 약 0.95초
            bool vanished = false, back = false, glinted = false;
            int slashes = 0;
            for (b.T = 0f; b.T < profile + glint + cuts + finish; b.T += Step())
            {
                float t = b.T;
                if (t < profile)
                {
                    // 허리 높이 옆모습 → 눈가로 천천히 다가감. 낮게 웅크리고 단검을 역수로
                    float u = Mathf.SmoothStep(0f, 1f, t / profile);
                    Vector3 eye = c + r * Mathf.Lerp(3.6f, 2.4f, u) * S + f * 0.6f * S + up * Mathf.Lerp(0.7f, 1.2f, u) * S;
                    Shot(b, eye, c + up * Mathf.Lerp(0.85f, 1.2f, u) * S + f * 0.2f * S, Mathf.Lerp(0.85f, 0.6f, u), 0.2f);
                    Pose(new Vector3(-0.5f, -0.2f, -0.6f), Vector3.up, 0.85f);
                    BodyTwist = 25f;
                    Crouch = 0.8f;
                    BodyLean = 12f;
                    if (Mathf.Repeat(t, 0.12f) < Step())
                        Fx.Unscaled(Fx.Play("Electro hit", c + Random.insideUnitSphere * 0.8f * S + up * 0.8f * S, Quaternion.identity, 0.25f * S, 0.1f, false));
                }
                else if (t < profile + glint)
                {
                    // 눈높이에 섬광 한 줄 → 사라짐
                    if (!glinted) { glinted = true; b.UI.SlashLine(0f, 60f, new Color(1f, 0.6f, 0.95f), 0.35f, 6f); LightFlash.Spawn(c + up * 1.2f * S, b.Color, 8f, 4f * S, 0.3f, true); }
                    if (!vanished && t > profile + glint * 0.6f)
                    {
                        vanished = true;
                        Fx.Unscaled(Fx.Play("Teleport", c, Quaternion.identity, 0.6f * S, 0.15f, false));
                        SetBodyVisible(false);
                    }
                }
                else if (t < profile + glint + cuts)
                {
                    // 빈 화면을 번개 일섬 다섯 줄이 가른다
                    float u = (t - profile - glint) / cuts;
                    Vector3 eye = c + r * 1.6f * S + f * 0.6f * S + up * 1.1f * S;
                    Shot(b, eye, c + f * 1.5f * S + up * 0.9f * S, 0.75f, 0.8f);
                    int want = Mathf.Min(5, 1 + (int)(u * 5f));
                    while (slashes < want)
                    {
                        float ang = new[] { 32f, -28f, 12f, -45f, 0f }[slashes];
                        b.UI.SlashLine(ang, Random.Range(-120f, 120f), slashes % 2 == 0 ? Color.white : new Color(1f, 0.55f, 0.95f), 0.25f, slashes == 4 ? 10f : 5f);
                        Vector3 mid = c + f * (1f + slashes * 0.4f) * S + up * 0.9f * S;
                        Vector3 side = Quaternion.AngleAxis(ang, f) * r;
                        Bolt(mid - side * 2.5f * S, mid + side * 2.5f * S, Color.Lerp(b.Color, Color.white, 0.75f), 0.035f, 0.25f);
                        LightFlash.Spawn(mid, b.Color, 6f, 3f * S, 0.15f, true);
                        PostFx.Pulse(0.5f, b.Color);
                        m_Player.Shake(0.1f, 0.15f);
                        slashes++;
                    }
                }
                else
                {
                    // 뒤에서 다시 나타나 마무리 — 단검을 털어 넣음
                    float u = GeoFunctions.EaseOutCubic((t - profile - glint - cuts) / finish);
                    if (!back)
                    {
                        back = true;
                        SetBodyVisible(true);
                        Fx.Unscaled(Fx.Play("Teleport", c, Quaternion.identity, 0.6f * S, 0.15f, false));
                        Release(b, c + f * 2f * S, 6f * S, 0.6f);
                        Fx.Unscaled(Fx.Play("Electro slash", c + f * 2f * S + up * 0.8f * S, Facing(-30f), 1.1f * S, 0.3f, false));
                    }
                    Pose(new Vector3(0.6f, -0.3f, 0.6f), Vector3.up, 1f);
                    BodyTwist = -15f;
                    Crouch = 0.4f;
                    Shot(b, c - f * Mathf.Lerp(1.5f, 3f, u) * S + r * 0.8f * S + up * Mathf.Lerp(1.1f, 2.2f, u) * S, c + f * 2f * S + up * 0.8f * S, Mathf.Lerp(0.7f, 0.95f, u), 1f);
                }
                Lights(b, back ? 0f : 3f, back ? 0f : 3.5f);
                DebugTick?.Invoke(t);
                yield return null;
            }
        }

        // ================================================================== 황금 명사수: 정오의 결투
        //  ① 허리춤 클로즈업 — 화면 위아래 검은 띠, 총에 손을 얹고 숨을 고름 (천천히 당겨짐)
        //  ② 순간 뽑기 — 어깨 너머로 휙, 총구가 표적을 겨누고 탕!
        //  ③ 총알 시점 — 카메라가 금빛 총알을 뒤따라 나선으로 돌며 날아감 (공기 고리가 뒤로 퍼짐)
        //  ④ 명중 — 표적에서 금빛 섬광·방사선
        //  ⑤ 돌아와서 — 총을 빙글 돌리고 총구 연기, 금화가 쏟아짐
        IEnumerator GoldBurst(Burst b)
        {
            Vector3 c = b.Center, f = b.Fwd, r = b.Right, up = Vector3.up;
            TrainingDummy tgt = FindTarget(14f);
            Vector3 aimPt = tgt ? tgt.Center : c + f * 7f * S + up * 1.1f * S;
            Fx.Unscaled(Fx.Play(b.Theme.Circle, Feet, Facing(), b.Theme.CircleSize * S, 1.9f, false));
            b.UI.Letterbox(true);

            const float draw = 0.5f, quick = 0.16f, fly = 0.55f, hit = 0.14f, after = 0.42f;
            bool glint = false, fired = false, impacted = false, coins = false;
            GameObject bullet = null;
            LineRenderer trail = null;
            Vector3 shotFrom = Vector3.zero, shotTo = aimPt, shotDir = f;
            var rings = new List<(LineRenderer lr, float born)>();
            float nextRing = 0f, nextSpark = 0f;

            for (b.T = 0f; b.T < draw + quick + fly + hit + after; b.T += Step())
            {
                float t = b.T;
                if (t < draw)
                {
                    // ① 허리춤 클로즈업: 총은 아래로, 카메라는 손 옆에서 천천히 다가감
                    float u = Mathf.SmoothStep(0f, 1f, t / draw);
                    Pose(new Vector3(0.3f, -0.95f, 0.15f), Vector3.forward, 0.7f);
                    BodyTwist = 10f;
                    Vector3 hand = c + r * 0.25f * S + up * 0.8f * S + f * 0.1f * S;
                    Shot(b, hand + r * Mathf.Lerp(1.2f, 0.85f, u) * S + f * Mathf.Lerp(0.9f, 0.6f, u) * S + up * 0.05f * S, hand, Mathf.Lerp(0.6f, 0.48f, u));
                    if (!glint && t > draw * 0.55f)
                    {
                        glint = true;
                        Fx.Unscaled(Fx.Play("Sparks flashing yellow", Tip, Quaternion.identity, 0.3f * S, 0.15f, false));
                        LightFlash.Spawn(Tip, b.Color, 5f, 1.5f * S, 0.25f, true);
                    }
                }
                else if (t < draw + quick)
                {
                    // ② 순간 뽑기: 총이 휙 올라와 표적을 겨눔, 카메라는 어깨 너머로
                    float u = GeoFunctions.EaseOutCubic((t - draw) / quick);
                    Vector3 aimLocal = transform.InverseTransformDirection((aimPt - Chest).normalized);
                    Pose(Vector3.Slerp(new Vector3(0.3f, -0.95f, 0.15f), aimLocal, u).normalized, Vector3.up, Mathf.Lerp(0.7f, 0.98f, u));
                    BodyTwist = Mathf.Lerp(10f, -6f, u);
                    Vector3 eye = c - f * 1.15f * S + r * 0.55f * S + up * 1.45f * S;
                    Shot(b, Vector3.Lerp(c + r * 1.1f * S + f * 0.8f * S + up * 0.85f * S, eye, u), Vector3.Lerp(c + up * 0.8f * S, aimPt, u), Mathf.Lerp(0.5f, 0.62f, u), 0.3f);
                }
                else if (t < draw + quick + fly)
                {
                    if (!fired)
                    {
                        // 탕!
                        fired = true;
                        Recoil = 1f;
                        shotFrom = Tip;
                        shotDir = (aimPt - shotFrom).normalized;
                        float len = (aimPt - shotFrom).magnitude;
                        if (Physics.Raycast(shotFrom, shotDir, out RaycastHit wall, len, ~(1 << 2), QueryTriggerInteraction.Ignore)) shotTo = wall.point;
                        b.UI.Flash(0.35f);
                        b.UI.RadialLines(new Color(1f, 0.85f, 0.35f, 0.9f), 20);
                        PostFx.Pulse(0.8f, b.Color);
                        m_Player.Shake(0.1f, 0.15f);
                        Fx.Unscaled(Fx.Play("Holy hit", shotFrom, Quaternion.LookRotation(shotDir), 0.6f * S, 0.15f, false));
                        Fx.Unscaled(Fx.Play("Smoke puff", shotFrom, Quaternion.identity, 0.3f * S, 0.3f, false));
                        LightFlash.Spawn(shotFrom, b.Color, 10f, 3f * S, 0.25f, true);

                        bullet = Ball(0.06f * S, new Color(1f, 0.72f, 0.2f));
                        bullet.transform.localScale = new Vector3(0.6f, 0.6f, 2.6f) * 0.06f * S;
                        var bm = bullet.GetComponent<Renderer>().material;
                        bm.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.1f) * 0.8f);
                        var glow = MakeLight(shotFrom, b.Color, 6f, 1.6f * S);
                        glow.transform.SetParent(bullet.transform, true);
                        trail = NewLine(0.035f * S, new Color(1f, 0.9f, 0.5f, 0.9f), 2);
                    }
                    // ③ 총알 시점: 총알 뒤를 나선으로 돌며 따라감
                    float u = (t - draw - quick) / fly;
                    Vector3 p = Vector3.Lerp(shotFrom, shotTo, u * 0.94f);
                    bullet.transform.SetPositionAndRotation(p, Quaternion.LookRotation(shotDir));
                    trail.SetPosition(0, Vector3.Lerp(shotFrom, p, Mathf.Max(0f, u - 0.35f) / Mathf.Max(0.01f, u)));
                    trail.SetPosition(1, p);
                    Vector3 side = Vector3.Cross(up, shotDir).normalized;
                    if (side.sqrMagnitude < 0.01f) side = r;
                    Quaternion roll = Quaternion.AngleAxis(u * 220f, shotDir);
                    Vector3 offset = roll * (side * 0.4f * S + up * 0.15f * S);
                    Vector3 eyeB = p - shotDir * Mathf.Lerp(0.9f, 1.2f, u) * S + offset;
                    if (b.Cam)
                    {
                        b.Cam.transform.SetPositionAndRotation(eyeB, Quaternion.LookRotation(p + shotDir * 1.2f * S - eyeB, roll * up));
                        b.Cam.fieldOfView = b.Fov0 * Mathf.Lerp(0.7f, 0.95f, u);
                    }
                    if (t >= nextRing)
                    {
                        nextRing = t + 0.07f;
                        var ring = NewLine(0.012f * S, new Color(1f, 0.92f, 0.6f, 0.8f), 20);
                        ring.loop = true;
                        Vector3 a1 = side, a2 = Vector3.Cross(shotDir, side);
                        for (int k = 0; k < 20; k++)
                        {
                            float ang = k / 20f * Mathf.PI * 2f;
                            ring.SetPosition(k, p + (Mathf.Cos(ang) * a1 + Mathf.Sin(ang) * a2) * 0.05f * S);
                        }
                        rings.Add((ring, t));
                    }
                    if (t >= nextSpark)
                    {
                        nextSpark = t + 0.05f;
                        Fx.Unscaled(Fx.Play("Sparks flashing yellow", p, Quaternion.identity, 0.12f * S, 0.1f, false));
                    }
                }
                else if (t < draw + quick + fly + hit)
                {
                    // ④ 명중
                    if (!impacted)
                    {
                        impacted = true;
                        if (bullet) Destroy(bullet);
                        if (trail) Destroy(trail.gameObject);
                        Vector3 ground = new Vector3(shotTo.x, GeoFx.GroundY(shotTo, S), shotTo.z);
                        Release(b, ground, 5f * S, 0.45f);
                        b.UI.RadialLines(new Color(1f, 0.9f, 0.45f, 0.95f), 28);
                        Fx.Unscaled(Fx.Play("Holy hit", shotTo, Quaternion.LookRotation(-shotDir), 0.75f * S, 0.2f, false));
                        Fx.Unscaled(Fx.Play("Sparks explode yellow", shotTo, Quaternion.identity, 0.9f * S, 0.2f, false));
                        Fx.Unscaled(Fx.Play("Star hit", shotTo, Quaternion.identity, 0.8f * S, 0.2f, false));
                    }
                    Vector3 side = Vector3.Cross(up, shotDir).normalized;
                    Shot(b, shotTo - shotDir * 2.4f * S + side * 1.1f * S + up * 0.45f * S, shotTo, 0.85f, 1f);
                }
                else
                {
                    // ⑤ 돌아와서: 총을 빙글 돌리고 위로 세워 총구 연기
                    float u = (t - draw - quick - fly - hit) / after;
                    if (!coins) { coins = true; b.UI.Coins(26); Fx.Unscaled(Fx.Play("Smoke puff", Tip, Quaternion.identity, 0.3f * S, 0.4f, false)); }
                    float g = Mathf.Min(1f, u / 0.6f) * Mathf.PI * 4f;
                    if (u < 0.6f) Pose(new Vector3(0.35f, 0.3f + Mathf.Sin(g) * 0.5f, Mathf.Cos(g) * 0.6f), new Vector3(0f, Mathf.Cos(g), -Mathf.Sin(g)), 0.8f);
                    else Pose(new Vector3(0.25f, 1f, 0.1f), Vector3.back, 0.75f);
                    BodyTwist = -8f;
                    float e = GeoFunctions.EaseOutCubic(u);
                    Shot(b, c + f * Mathf.Lerp(2f, 2.5f, e) * S + r * Mathf.Lerp(0.7f, 1f, e) * S + up * Mathf.Lerp(1.25f, 1.45f, e) * S, c + up * 1.25f * S, Mathf.Lerp(0.6f, 0.7f, e));
                    LookTarget = b.Cam ? b.Cam.transform.position : (Vector3?)null;
                }

                // 공기 고리: 뒤로 남아 커지며 사라짐
                for (int i = rings.Count - 1; i >= 0; i--)
                {
                    var (lr, born) = rings[i];
                    float a = (t - born) / 0.35f;
                    if (!lr || a >= 1f) { if (lr) Destroy(lr.gameObject); rings.RemoveAt(i); continue; }
                    Vector3 mid = Vector3.zero;
                    for (int k = 0; k < lr.positionCount; k++) mid += lr.GetPosition(k);
                    mid /= lr.positionCount;
                    float grow = 1f + Time.unscaledDeltaTime * 9f;
                    for (int k = 0; k < lr.positionCount; k++) lr.SetPosition(k, mid + (lr.GetPosition(k) - mid) * grow);
                    Color col = lr.startColor; col.a = 0.8f * (1f - a);
                    lr.startColor = lr.endColor = col;
                }

                // 허리춤 클로즈업은 가까워서 조명을 약하게 (하얗게 날아가지 않게)
                Lights(b, impacted ? 0f : (t < draw ? 0.9f : 2.5f), impacted ? 0f : (t < draw ? 1.2f : 2.5f));
                DebugTick?.Invoke(t);
                yield return null;
            }
            foreach (var (lr, _) in rings) if (lr) Destroy(lr.gameObject);
            if (bullet) Destroy(bullet);
            if (trail) Destroy(trail.gameObject);
            LookTarget = null;
            b.UI.Letterbox(false);
        }

        static LineRenderer NewLine(float width, Color color, int points)
        {
            var go = new GameObject("Burst Line");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = GeoFx.LineMaterial;
            lr.positionCount = points;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = color;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        // 카메라가 벽·지형 속으로 들어가지 않게
        Vector3 Clamp(Vector3 eye, Vector3 look)
        {
            Vector3 back = eye - look;
            if (Physics.SphereCast(look, 0.15f * S, back.normalized, out RaycastHit block, back.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return look + back.normalized * Mathf.Max(0.5f * S, block.distance - 0.1f * S);
            return eye;
        }

        static Light MakeLight(Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("Burst Light");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ 컷신 화면 겹침 (서리·일섬·방사선·금화)
        class BurstUI
        {
            readonly Canvas m_Canvas;
            readonly RectTransform m_Root;
            readonly Image m_Flash;
            readonly CanvasGroup m_Name;
            readonly List<RectTransform> m_Frost = new List<RectTransform>();

            public BurstUI(NewModeCombat c, SkillDef skill, Color color)
            {
                m_Canvas = UIUtil.Canvas(c.transform, "Burst", 30);
                m_Root = (RectTransform)m_Canvas.transform;

                var name = new GameObject("Name", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                name.SetParent(m_Root, false);
                name.anchorMin = name.anchorMax = new Vector2(0f, 0f);
                name.pivot = new Vector2(0f, 0f);
                name.sizeDelta = new Vector2(800f, 120f);
                m_Name = name.GetComponent<CanvasGroup>();
                var d = Gs.Diamond(name, color, 26f);
                d.anchorMin = d.anchorMax = new Vector2(0f, 0.5f);
                d.anchoredPosition = new Vector2(20f, 8f);
                d.gameObject.AddComponent<Outline>().effectColor = Gs.Gold;
                var t = Gs.Text(name, $"<b>{skill.Name}</b>", 54f, TextAlignmentOptions.Left, Color.white, true);
                UIUtil.Stretch(t.rectTransform, 0f);
                t.rectTransform.offsetMin = new Vector2(56f, 30f);
                var sub = Gs.Text(name, WeaponCatalog.Current.ClassName, 24f, TextAlignmentOptions.Left, Color.Lerp(color, Gs.GoldBright, 0.5f), true);
                UIUtil.Stretch(sub.rectTransform, 0f);
                sub.rectTransform.offsetMin = new Vector2(58f, 0f);
                sub.rectTransform.offsetMax = new Vector2(0f, -78f);
                var line = UIUtil.Image(name, "Line", new Color(color.r, color.g, color.b, 0.8f));
                line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(0.7f, 0f);
                line.sizeDelta = new Vector2(0f, 3f);
                line.anchoredPosition = new Vector2(56f, 30f);
                m_Name.alpha = 0f;
                name.anchoredPosition = new Vector2(20f, 70f);
                DOTween.Sequence()
                    .Append(m_Name.DOFade(1f, 0.25f))
                    .Join(name.DOAnchorPosX(70f, 0.35f).SetEase(Ease.OutCubic))
                    .SetUpdate(true).SetLink(name.gameObject);

                m_Flash = UIUtil.Image(m_Root, "Flash", new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
                UIUtil.Stretch(m_Flash.rectTransform, 0f);
            }

            public void Flash(float alpha = 0.75f)
            {
                m_Flash.DOKill();
                m_Flash.color = new Color(1f, 1f, 1f, alpha);
                m_Flash.DOFade(0f, 0.3f).SetUpdate(true).SetLink(m_Flash.gameObject);
                m_Flash.transform.SetAsLastSibling();
            }

            // 화면을 가로지르는 일섬 (각도·세로 위치·두께)
            public void SlashLine(float angle, float y, Color color, float time, float thickness = 4f)
            {
                var line = UIUtil.Image(m_Root, "Slash", color);
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
                line.anchoredPosition = new Vector2(0f, y);
                line.sizeDelta = new Vector2(2600f, thickness);
                line.localRotation = Quaternion.Euler(0f, 0f, angle);
                line.localScale = new Vector3(0f, 1f, 1f);
                var img = line.GetComponent<Image>();
                DOTween.Sequence()
                    .Append(line.DOScaleX(1f, time * 0.4f).SetEase(Ease.OutCubic))
                    .Join(line.DOScaleY(2.5f, time * 0.4f))
                    .Append(img.DOFade(0f, time * 0.6f))
                    .Join(line.DOScaleY(0.2f, time * 0.6f))
                    .SetUpdate(true).SetLink(line.gameObject)
                    .OnComplete(() => Object.Destroy(line.gameObject));
            }

            // 가운데에서 뻗어 나가는 방사선 (총성·충격)
            public void RadialLines(Color color, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var l = UIUtil.Image(m_Root, "Ray", color);
                    l.anchorMin = l.anchorMax = new Vector2(0.5f, 0.5f);
                    l.pivot = new Vector2(0f, 0.5f);
                    l.sizeDelta = new Vector2(Random.Range(300f, 700f), Random.Range(3f, 9f));
                    l.localRotation = Quaternion.Euler(0f, 0f, i * 360f / count + Random.Range(-5f, 5f));
                    l.anchoredPosition = Vector2.zero;
                    var img = l.GetComponent<Image>();
                    float dist = Random.Range(500f, 1100f);
                    Vector2 dir = new Vector2(Mathf.Cos(l.localEulerAngles.z * Mathf.Deg2Rad), Mathf.Sin(l.localEulerAngles.z * Mathf.Deg2Rad));
                    DOTween.Sequence()
                        .Append(l.DOAnchorPos(dir * dist, 0.35f).SetEase(Ease.OutCubic))
                        .Join(img.DOFade(0f, 0.35f).SetEase(Ease.InQuad))
                        .SetUpdate(true).SetLink(l.gameObject)
                        .OnComplete(() => Object.Destroy(l.gameObject));
                }
            }

            // 화면 네 귀퉁이에 서리가 낀다 (마름모 결정이 자라남)
            public void FrostEdges(Color color)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 anchor = new Vector2(corner % 2, corner / 2);
                    for (int i = 0; i < 14; i++)
                    {
                        var d = Gs.Diamond(m_Root, new Color(Mathf.Lerp(color.r, 1f, 0.5f), Mathf.Lerp(color.g, 1f, 0.5f), 1f, Random.Range(0.25f, 0.6f)), Random.Range(40f, 150f));
                        d.anchorMin = d.anchorMax = anchor;
                        Vector2 inward = new Vector2(anchor.x < 0.5f ? 1f : -1f, anchor.y < 0.5f ? 1f : -1f);
                        d.anchoredPosition = new Vector2(Random.Range(0f, 320f) * inward.x, Random.Range(0f, 200f) * inward.y);
                        d.localScale = Vector3.zero;
                        d.DOScale(Random.Range(0.6f, 1.2f), Random.Range(0.25f, 0.6f)).SetDelay(Random.Range(0f, 0.3f)).SetEase(Ease.OutBack).SetUpdate(true).SetLink(d.gameObject);
                        m_Frost.Add(d);
                    }
                }
            }

            public void FrostShatter()
            {
                foreach (var d in m_Frost)
                {
                    if (!d) continue;
                    Vector2 away = (d.anchoredPosition + new Vector2(d.anchorMin.x < 0.5f ? -1f : 1f, d.anchorMin.y < 0.5f ? -1f : 1f) * 50f).normalized * Random.Range(200f, 500f);
                    var img = d.GetComponent<Image>();
                    var dd = d;
                    DOTween.Sequence()
                        .Append(d.DOAnchorPos(d.anchoredPosition + away, 0.4f).SetEase(Ease.OutCubic))
                        .Join(d.DORotate(new Vector3(0f, 0f, Random.Range(-180f, 180f)), 0.4f, RotateMode.LocalAxisAdd))
                        .Join(img.DOFade(0f, 0.4f))
                        .SetUpdate(true).SetLink(d.gameObject)
                        .OnComplete(() => Object.Destroy(dd.gameObject));
                }
                m_Frost.Clear();
            }

            // 금화가 쏟아진다
            public void Coins(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var coin = UIUtil.RoundedImage(m_Root, "Coin", new Color32(255, 205, 80, 255), true);
                    coin.anchorMin = coin.anchorMax = new Vector2(0.5f, 1f);
                    float x = Random.Range(-900f, 900f);
                    coin.anchoredPosition = new Vector2(x, Random.Range(40f, 300f));
                    float size = Random.Range(22f, 46f);
                    coin.sizeDelta = new Vector2(size, size);
                    coin.gameObject.AddComponent<Outline>().effectColor = new Color32(150, 100, 20, 255);
                    var c = coin;
                    DOTween.Sequence()
                        .Append(coin.DOAnchorPosY(-1200f, Random.Range(0.9f, 1.6f)).SetEase(Ease.InQuad).SetDelay(Random.Range(0f, 0.5f)))
                        .Join(coin.DOScaleX(0.2f, 0.15f).SetLoops(10, LoopType.Yoyo))
                        .SetUpdate(true).SetLink(coin.gameObject)
                        .OnComplete(() => Object.Destroy(c.gameObject));
                }
            }

            // 영화처럼 화면 위아래 검은 띠
            RectTransform m_BarTop, m_BarBottom;
            public void Letterbox(bool on)
            {
                if (!m_BarTop)
                {
                    m_BarTop = UIUtil.Image(m_Root, "Bar", Color.black);
                    m_BarTop.anchorMin = new Vector2(0f, 1f); m_BarTop.anchorMax = new Vector2(1f, 1f); m_BarTop.pivot = new Vector2(0.5f, 1f);
                    m_BarBottom = UIUtil.Image(m_Root, "Bar", Color.black);
                    m_BarBottom.anchorMin = new Vector2(0f, 0f); m_BarBottom.anchorMax = new Vector2(1f, 0f); m_BarBottom.pivot = new Vector2(0.5f, 0f);
                    m_BarTop.sizeDelta = m_BarBottom.sizeDelta = new Vector2(0f, 0f);
                    m_BarTop.SetAsFirstSibling(); m_BarBottom.SetAsFirstSibling();
                }
                float h = on ? 120f : 0f;
                m_BarTop.DOSizeDelta(new Vector2(0f, h), 0.2f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(m_BarTop.gameObject);
                m_BarBottom.DOSizeDelta(new Vector2(0f, h), 0.2f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(m_BarBottom.gameObject);
            }

            public void Close()
            {
                m_Name.DOFade(0f, 0.25f).SetUpdate(true).SetLink(m_Name.gameObject);
                Object.Destroy(m_Canvas.gameObject, 0.8f);
            }
        }
    }
}
