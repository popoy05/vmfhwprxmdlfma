using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // "최종 보스 처치" 목표. 모든 난이도에 있다.
    // 나머지 임무(적 처치·관문·좌표)를 모두 끝내면 보스가 나타나고, 보스를 쓰러뜨려야 승리한다.
    public class BossObjective : Objective
    {
        MathModeManager m_Manager;
        EnemyController m_Boss;
        bool m_Summoning, m_Spawned;
        float m_NextCheck;

        public void Setup(MathModeManager manager)
        {
            m_Manager = manager;
            Title = $"최종 보스: {MathEnemy.NameOf(EnemyKind.Boss)} 처치";
            Description = "다른 임무를 모두 끝내면 나타난다";
        }

        void Update()
        {
            if (IsCompleted || m_Summoning || Time.time < m_NextCheck) return;
            m_NextCheck = Time.time + 0.5f;

            if (m_Spawned)
            {
                // 보스가 맵 밖으로 떨어지는 등 처치 없이 사라졌다면 다시 부른다
                if (!m_Boss) StartCoroutine(Summon());
                return;
            }

            foreach (Objective o in FindObjectsByType<Objective>())
                if (o != this && !o.IsOptional && !o.IsCompleted)
                    return;

            StartCoroutine(Summon());
        }

        IEnumerator Summon()
        {
            m_Summoning = true;
            MathModeManager.ShowMessage("경고! 최종 보스가 다가온다...");
            UpdateObjective("곧 나타난다!", "!", "");
            if (m_Manager.CameraFX) m_Manager.CameraFX.Shake(0.5f, 1.5f);
            yield return new WaitForSeconds(3f);

            Vector3 spot = m_Manager.Player.position + m_Manager.Player.forward * 18f;
            if (m_Manager.World != null)
            {
                float oldMax = m_Manager.World.MaxFromPlayer;
                m_Manager.World.MaxFromPlayer = 40f;
                if (m_Manager.World.TryFindSpot(m_Manager.Player.position, new List<Vector3>(), 16f, 0f, 3f, out Vector3 found))
                    spot = found;
                m_Manager.World.MaxFromPlayer = oldMax;
            }

            m_Boss = m_Manager.SpawnEnemy(EnemyKind.Boss, spot, 4f);
            if (!m_Boss)
            {
                // 보스를 만들 수 없으면 게임이 막히지 않도록 완료 처리
                CompleteObjective(string.Empty, "-", "목표 완료 : " + Title);
                yield break;
            }

            var health = m_Boss.GetComponent<Health>();
            health.MaxHealth = m_Manager.Preset.BossHealth;
            health.CurrentHealth = health.MaxHealth;
            health.OnDie += OnBossDied;
            m_Boss.gameObject.AddComponent<BossController>().Init(m_Manager, m_Boss);

            if (m_Manager.CameraFX)
            {
                m_Manager.CameraFX.Shake(1.2f, 0.8f);
                m_Manager.CameraFX.Flash(new Color(1f, 0.8f, 0.2f, 0.35f), 0.6f);
            }

            MathModeManager.ShowMessage($"최종 보스 '{MathEnemy.NameOf(EnemyKind.Boss)}' 출현! 원 탄막 사이로 피하며 공격하라");
            UpdateObjective("보스를 쓰러뜨려라!", "전투 중", "");
            m_Spawned = true;
            m_Summoning = false;
        }

        void OnBossDied()
        {
            if (IsCompleted) return;
            MathModeManager.ShowMessage("최종 보스 격파! ∞ 도 결국 극한값을 가진다");
            CompleteObjective(string.Empty, "격파", "목표 완료 : " + Title);
        }
    }

    // 보스 공격 패턴. 체력에 따라 3단계로 강해진다.
    //  1단계 (함수)  : 원형 탄막
    //  2단계 (미분)  : + 나선 탄막, 바닥 강타(빨간 원), 돌격병 소환
    //  3단계 (적분)  : 모든 패턴이 빨라지고 부채꼴 조준 사격 추가
    public class BossController : MonoBehaviour
    {
        static readonly string[] k_PhaseNames = { "", "1단계 · 함수 f(x)", "2단계 · 미분 f'(x)", "3단계 · 적분 ∫f(x)dx" };

        MathModeManager m_Manager;
        EnemyController m_Enemy;
        Health m_Health;
        int m_Phase = 1;
        float m_NextRing, m_NextSpiral, m_NextSlam, m_NextSummon, m_NextFan;
        float m_RingAngle;
        readonly List<EnemyController> m_Summons = new List<EnemyController>();

        // 체력 바
        GameObject m_BarRoot;
        RectTransform m_BarFill, m_BarLag;
        TextMeshProUGUI m_BarText;
        float m_LagRatio = 1f;

        public void Init(MathModeManager manager, EnemyController enemy)
        {
            m_Manager = manager;
            m_Enemy = enemy;
            m_Health = enemy.GetComponent<Health>();
            float now = Time.time;
            m_NextRing = now + 2f;
            m_NextSpiral = now + 6f;
            m_NextSlam = now + 8f;
            m_NextSummon = now + 10f;
            m_NextFan = now + 4f;
            BuildBar();
            m_Health.OnDie += () => { if (m_BarRoot) Destroy(m_BarRoot); };
        }

        void OnDestroy()
        {
            if (m_BarRoot) Destroy(m_BarRoot);
        }

        float Ratio => m_Health.MaxHealth > 0f ? m_Health.CurrentHealth / m_Health.MaxHealth : 0f;
        Transform Player => m_Manager.Player;
        Vector3 Origin => transform.position + Vector3.up * 1.1f; // 플레이어 가슴 높이로 쏜다

        void Update()
        {
            UpdateBar();
            if (!Player || m_Health.CurrentHealth <= 0f) return;

            int phase = Ratio > 0.66f ? 1 : Ratio > 0.33f ? 2 : 3;
            if (phase != m_Phase)
            {
                m_Phase = phase;
                MathModeManager.ShowMessage($"보스 {k_PhaseNames[phase]} — 패턴이 강해진다!");
                if (m_Manager.CameraFX) m_Manager.CameraFX.Shake(0.8f, 0.5f);
                if (phase == 3) MathEnemy.ScaleSpeed(m_Enemy, 1.35f);
            }

            if (Vector3.Distance(transform.position, Player.position) > 60f) return;
            float speed = m_Phase == 3 ? 0.7f : 1f; // 3단계는 쿨타임 30% 감소
            float now = Time.time;

            if (now >= m_NextRing)
            {
                m_NextRing = now + (m_Phase == 1 ? 3.2f : 2.6f) * speed;
                Ring(m_Phase == 1 ? 14 : 18, m_Phase == 1 ? 9f : 11f);
            }

            if (m_Phase >= 2 && now >= m_NextSpiral)
            {
                m_NextSpiral = now + 8f * speed;
                StartCoroutine(Spiral());
            }

            if (m_Phase >= 2 && now >= m_NextSlam)
            {
                m_NextSlam = now + 9f * speed;
                StartCoroutine(Slam(Player.position));
            }

            if (m_Phase >= 2 && now >= m_NextSummon)
            {
                m_NextSummon = now + 15f * speed;
                Summon();
            }

            if (m_Phase == 3 && now >= m_NextFan)
            {
                m_NextFan = now + 2.2f;
                Fan();
            }
        }

        void Ring(int count, float speed)
        {
            m_RingAngle += 360f / count * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float a = (m_RingAngle + 360f * i / count) * Mathf.Deg2Rad;
                EnemyBullet.Fire(m_Manager, gameObject, Origin, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * speed, 10f, new Color(1f, 0.75f, 0.15f), 0.6f);
            }
        }

        IEnumerator Spiral()
        {
            MathModeManager.ShowMessage("보스: 나선 r = θ 탄막!");
            float angle = Random.Range(0f, 360f);
            for (float t = 0f; t < 2.2f; t += 0.09f)
            {
                if (!this || m_Health.CurrentHealth <= 0f) yield break;
                angle += 17f;
                for (int arm = 0; arm < 3; arm++)
                {
                    float a = (angle + arm * 120f) * Mathf.Deg2Rad;
                    EnemyBullet.Fire(m_Manager, gameObject, Origin, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10f, 8f, new Color(1f, 0.4f, 0.2f), 0.45f);
                }

                yield return new WaitForSeconds(0.09f);
            }
        }

        // 플레이어가 있던 자리에 빨간 원 → 1.3초 뒤 빛기둥
        IEnumerator Slam(Vector3 target)
        {
            const float radius = 4.5f, delay = 1.3f;
            LineRenderer ring = m_Manager.CreateLine(null, new Color(2f, 0.3f, 0.2f), 0.15f);
            ring.loop = true;
            ring.positionCount = 40;
            LineRenderer fill = m_Manager.CreateLine(null, new Color(2f, 0.6f, 0.2f), 0.1f);
            fill.loop = true;
            fill.positionCount = 40;
            for (float t = 0f; t < delay; t += Time.deltaTime)
            {
                Circle(ring, target, radius);
                Circle(fill, target, radius * t / delay);
                yield return null;
            }

            Destroy(fill.gameObject);
            var beam = m_Manager.CreateBlock(null, PrimitiveType.Cylinder, target + Vector3.up * 20f, new Vector3(radius * 2f, 20f, radius * 2f), new Color(2f, 1f, 0.4f));
            if (m_Manager.ExplosionVfx) Destroy(Instantiate(m_Manager.ExplosionVfx, target, Quaternion.identity), 3f);
            if (Player && Vector3.Distance(new Vector3(Player.position.x, target.y, Player.position.z), target) < radius &&
                Mathf.Abs(Player.position.y - target.y) < 3f)
            {
                var damageable = Player.GetComponent<Damageable>();
                if (damageable) damageable.InflictDamage(28f, false, gameObject);
            }

            if (m_Manager.CameraFX) m_Manager.CameraFX.Shake(0.6f, 0.3f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = 1f - t / 0.35f;
                beam.transform.localScale = new Vector3(radius * 2f * k, 20f, radius * 2f * k);
                yield return null;
            }

            Destroy(beam);
            Destroy(ring.gameObject);
        }

        void Summon()
        {
            m_Summons.RemoveAll(e => !e);
            if (m_Summons.Count >= 4) return;
            MathModeManager.ShowMessage("보스가 벡터 돌격병을 소환했다!");
            for (int i = 0; i < 2; i++)
            {
                EnemyController e = m_Manager.SpawnEnemy(m_Phase == 3 && i == 1 ? EnemyKind.Blinker : EnemyKind.Rusher,
                    transform.position + transform.right * (i == 0 ? 4f : -4f), 3f);
                if (e) m_Summons.Add(e);
            }
        }

        // 플레이어 쪽으로 5발 부채꼴
        void Fan()
        {
            Vector3 to = Player.position + Vector3.up * 1.1f - Origin;
            to.y = 0f;
            for (int i = -2; i <= 2; i++)
                EnemyBullet.Fire(m_Manager, gameObject, Origin, Quaternion.Euler(0f, i * 12f, 0f) * to.normalized * 15f, 9f, new Color(1f, 0.2f, 0.5f), 0.45f);
        }

        static void Circle(LineRenderer line, Vector3 center, float r)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0.15f, Mathf.Sin(a) * r));
            }
        }

        // ------------------------------------------------------------------ 체력 바

        void BuildBar()
        {
            Canvas canvas = UIUtil.Canvas(null, "BossBar", 20);
            m_BarRoot = canvas.gameObject;
            TMP_FontAsset font = m_Manager.Font;

            var frame = UIUtil.RoundedImage(canvas.transform, "Frame", new Color(0.05f, 0.03f, 0.02f, 0.85f));
            UIUtil.Place(frame, new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(940f, 74f));
            var name = UIUtil.Text(frame, font, $"<b>{MathEnemy.NameOf(EnemyKind.Boss)}</b>", 26f, TextAlignmentOptions.Left);
            UIUtil.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(240f, -18f), new Vector2(440f, 32f));
            name.color = new Color(1f, 0.85f, 0.35f);
            m_BarText = UIUtil.Text(frame, font, "", 22f, TextAlignmentOptions.Right);
            UIUtil.Place(m_BarText.rectTransform, new Vector2(1f, 1f), new Vector2(-240f, -18f), new Vector2(440f, 32f));

            var back = UIUtil.Image(frame, "Back", new Color(0.2f, 0.05f, 0.05f, 1f));
            UIUtil.Place(back, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(900f, 20f));
            m_BarLag = UIUtil.Image(back, "Lag", new Color(1f, 0.95f, 0.8f, 0.9f));
            m_BarFill = UIUtil.Image(back, "Fill", new Color(1f, 0.6f, 0.1f, 1f));
            foreach (RectTransform rt in new[] { m_BarLag, m_BarFill })
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        void UpdateBar()
        {
            if (!m_BarFill) return;
            float r = Mathf.Clamp01(Ratio);
            m_LagRatio = Mathf.MoveTowards(m_LagRatio, r, Time.deltaTime * 0.4f);
            if (m_LagRatio < r) m_LagRatio = r;
            m_BarFill.anchorMax = new Vector2(r, 1f);
            m_BarLag.anchorMax = new Vector2(m_LagRatio, 1f);
            m_BarFill.GetComponent<Image>().color = m_Phase == 1 ? new Color(1f, 0.6f, 0.1f) : m_Phase == 2 ? new Color(1f, 0.35f, 0.1f) : new Color(0.9f, 0.1f, 0.25f);
            m_BarText.text = $"{k_PhaseNames[m_Phase]}   {Mathf.CeilToInt(m_Health.CurrentHealth)} / {Mathf.CeilToInt(m_Health.MaxHealth)}";
        }
    }
}
