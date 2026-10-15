using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 궁극기 [G] "Σ 무한급수 폭격"
    // 적 처치·관문 정답·좌표 발견과 시간 경과로 게이지가 찬다. 발동하면 컷신:
    //  ① 느린 화면 + 위아래 검은 띠 + 제목  ② 카메라가 플레이어 주변을 돌며 바닥에 수학 마법진
    //  ③ 하늘로 올라가 좌표 격자·사인 곡선  ④ 주변 적마다 빛기둥 폭격 (부분합 S(n) = 1 - 1/2ⁿ 이 1에 가까워짐)
    //  ⑤ 하얀 섬광과 함께 플레이어 시점으로 복귀
    public class MathUltimate : MonoBehaviour
    {
        const float k_MaxCharge = 100f;
        const float k_Radius = 50f;
        const float k_Damage = 250f;
        const int k_MaxTargets = 14;

        public float Charge01 => m_Charge / k_MaxCharge;
        public bool IsReady => m_Charge >= k_MaxCharge && !IsPlaying;
        public bool IsPlaying { get; private set; }

        MathModeManager m_Manager;
        float m_Charge;
        bool m_AnnouncedReady;

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
        }

        public void AddCharge(float amount)
        {
            if (IsPlaying) return;
            m_Charge = Mathf.Min(k_MaxCharge, m_Charge + amount * m_Manager.Preset.UltimateChargeRate);
        }

        void Update()
        {
            if (IsPlaying) return;
            AddCharge(Time.deltaTime); // 시간이 지나도 조금씩 찬다

            if (IsReady && !m_AnnouncedReady)
            {
                m_AnnouncedReady = true;
                MathModeManager.ShowMessage(VirtualInputActive ? "궁극기 준비 완료! 금색 Σ 버튼" : "궁극기 준비 완료! [G]");
            }

            bool canInput = m_Manager.PlayerInput && m_Manager.PlayerInput.CanProcessInput();
            if (canInput && MathInput.Pressed(Key.G, "ultimate"))
            {
                if (!TryActivate()) MathModeManager.ShowMessage($"궁극기 충전 중 {Charge01 * 100f:0}%");
            }
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            StartCoroutine(Cutscene());
            return true;
        }

        static bool VirtualInputActive => Unity.FPS.Gameplay.VirtualInput.Active;

        // ------------------------------------------------------------------ 컷신

        IEnumerator Cutscene()
        {
            IsPlaying = true;
            m_Charge = 0f;
            m_AnnouncedReady = false;
            GameStats.Ultimates++;

            Transform player = m_Manager.Player;
            Vector3 center = player.position;
            var input = m_Manager.PlayerInput;
            if (input) input.InputBlocked = true;
            if (m_Manager.PlayerHealth) m_Manager.PlayerHealth.Invincible = true;

            // 다른 화면 UI는 잠깐 숨긴다
            var hiddenCanvases = new List<Canvas>();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Canvas c in root.GetComponentsInChildren<Canvas>())
                if (c.enabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }

            // 카메라 교체
            Camera mainCam = Camera.main;
            Camera weaponCam = m_Manager.PlayerWeapons ? m_Manager.PlayerWeapons.WeaponCamera : null;
            var cineGo = new GameObject("UltimateCamera");
            Camera cine = cineGo.AddComponent<Camera>();
            if (mainCam)
            {
                cine.CopyFrom(mainCam);
                cine.cullingMask = mainCam.cullingMask;
                mainCam.enabled = false;
            }
            cine.fieldOfView = 55f;
            cine.depth = 50f;
            cine.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (weaponCam) weaponCam.enabled = false;

            // 컷신 UI: 검은 띠, 제목, 부분합, 섬광
            Canvas ui = UIUtil.Canvas(transform, "UltimateUI", 70);
            RectTransform topBar = Bar(ui.transform, true), bottomBar = Bar(ui.transform, false);
            TMP_FontAsset font = m_Manager.Font;
            var small = UIUtil.Text(ui.transform, font, "궁 극 기", 34f, TextAlignmentOptions.Center);
            var title = UIUtil.Text(ui.transform, font, "<b>Σ 무한급수 폭격</b>", 110f, TextAlignmentOptions.Center);
            var subtitle = UIUtil.Text(ui.transform, font, "1/2 + 1/4 + 1/8 + ... = 1   끝없이 더해도, 합은 유한하다", 30f, TextAlignmentOptions.Center);
            var sum = UIUtil.Text(ui.transform, font, "", 46f, TextAlignmentOptions.Center);
            PlaceText(small, new Vector2(0f, 150f), new Vector2(1200f, 60f), new Color(1f, 0.85f, 0.4f));
            PlaceText(title, new Vector2(0f, 60f), new Vector2(1600f, 160f), Color.white);
            PlaceText(subtitle, new Vector2(0f, -40f), new Vector2(1600f, 60f), new Color(0.75f, 0.9f, 1f));
            PlaceText(sum, new Vector2(0f, -330f), new Vector2(1400f, 80f), new Color(1f, 0.9f, 0.5f));
            title.outlineWidth = 0.2f;
            title.outlineColor = new Color(0.6f, 0.35f, 0f);
            var flash = UIUtil.Image(ui.transform, "Flash", new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
            UIUtil.Stretch((RectTransform)flash.transform, 0f);

            float oldFixed = Time.fixedDeltaTime;
            Time.timeScale = 0.15f;
            Time.fixedDeltaTime = oldFixed * 0.15f;

            var cleanup = new List<GameObject> { cineGo };
            Transform sigil = BuildSigil(center, cleanup);
            float startYaw = player.eulerAngles.y;

            // ① + ② 카메라가 돌며 마법진이 그려진다
            const float orbit = 1.7f;
            for (float t = 0f; t < orbit; t += Time.unscaledDeltaTime)
            {
                float u = t / orbit;
                float e = 1f - Mathf.Pow(1f - u, 2f);
                SetBars(topBar, bottomBar, Mathf.Clamp01(t / 0.3f));
                float a = (startYaw + 160f + 200f * e) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(6f, 11f, e), h = Mathf.Lerp(1.8f, 5.5f, e);
                cine.transform.position = center + new Vector3(Mathf.Sin(a) * r, h, Mathf.Cos(a) * r);
                cine.transform.LookAt(center + Vector3.up * 1.4f);

                DrawSigil(sigil, u);
                sigil.Rotate(0f, 40f * Time.unscaledDeltaTime, 0f);

                float titleIn = Mathf.Clamp01((t - 0.2f) / 0.25f);
                small.alpha = titleIn;
                title.alpha = titleIn;
                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.SmoothStep(0f, 1f, titleIn));
                subtitle.alpha = Mathf.Clamp01((t - 0.6f) / 0.4f);
                yield return null;
            }

            // ③ 하늘로 올라가 내려다보며 격자·사인 곡선
            List<LineRenderer> sky = BuildSkyGrid(center, cleanup);
            Vector3 fromPos = cine.transform.position;
            Quaternion fromRot = cine.transform.rotation;
            Vector3 topPos = center + new Vector3(0f, 60f, -18f);
            Quaternion topRot = Quaternion.LookRotation(center - topPos);
            const float rise = 0.9f;
            for (float t = 0f; t < rise; t += Time.unscaledDeltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / rise);
                cine.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, topPos, e), Quaternion.Slerp(fromRot, topRot, e));
                float fadeTitle = 1f - e;
                small.alpha = title.alpha = subtitle.alpha = fadeTitle;
                foreach (LineRenderer l in sky) l.widthMultiplier = Mathf.Lerp(0f, l.name == "Wave" ? 0.5f : 0.15f, e);
                sigil.Rotate(0f, 40f * Time.unscaledDeltaTime, 0f);
                yield return null;
            }

            // ④ 적마다 빛기둥 (부분합이 1에 다가간다)
            List<EnemyController> targets = FindTargets(center);
            sum.alpha = 1f;
            int n = 0;
            foreach (EnemyController enemy in targets)
            {
                if (!enemy) continue;
                n++;
                Vector3 p = enemy.transform.position;
                var beam = m_Manager.CreateBlock(null, PrimitiveType.Cylinder, p + Vector3.up * 30f, new Vector3(0.5f, 30f, 0.5f), new Color(2.2f, 1.6f, 0.6f));
                cleanup.Add(beam);
                var health = enemy.GetComponent<Health>();
                if (health) health.TakeDamage(k_Damage, m_Manager.Player.gameObject);
                if (m_Manager.ExplosionVfx)
                    Destroy(Instantiate(m_Manager.ExplosionVfx, p, Quaternion.identity), 4f);

                float s = 1f - Mathf.Pow(0.5f, n);
                sum.text = $"S({n}) = 1/2 + ... + 1/{Mathf.Pow(2, n):0} = <color=#FFFFFF>{s:0.0000}</color>";
                sum.rectTransform.localScale = Vector3.one * 1.25f;

                for (float t = 0f; t < 0.14f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.14f;
                    beam.transform.localScale = new Vector3(Mathf.Lerp(0.5f, 3.5f, k), 30f, Mathf.Lerp(0.5f, 3.5f, k));
                    cine.transform.position = topPos + Random.insideUnitSphere * 0.8f * (1f - k);
                    sum.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, k);
                    yield return null;
                }
            }

            if (n == 0) sum.text = "주변에 적이 없다!  S = 0";
            yield return WaitUnscaled(0.5f);

            // ⑤ 섬광과 함께 복귀
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1f, 1f, 1f, t / 0.15f);
                yield return null;
            }

            foreach (GameObject go in cleanup) if (go) Destroy(go);
            if (mainCam) mainCam.enabled = true;
            if (weaponCam) weaponCam.enabled = true;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = oldFixed;
            foreach (Canvas c in hiddenCanvases) if (c) c.enabled = true;
            if (input) input.InputBlocked = false;
            sum.text = small.text = title.text = subtitle.text = "";

            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.5f;
                flash.color = new Color(1f, 1f, 1f, 1f - k);
                SetBars(topBar, bottomBar, 1f - k);
                yield return null;
            }

            Destroy(ui.gameObject);
            MathModeManager.ShowMessage($"Σ 무한급수 폭격: 적 {n}기 명중!");
            IsPlaying = false;

            yield return new WaitForSeconds(1.5f);
            if (m_Manager.PlayerHealth) m_Manager.PlayerHealth.Invincible = false;
        }

        List<EnemyController> FindTargets(Vector3 center)
        {
            var manager = FindAnyObjectByType<EnemyManager>();
            if (!manager || manager.Enemies == null) return new List<EnemyController>();
            return manager.Enemies.Where(e => e && Vector3.Distance(e.transform.position, center) < k_Radius)
                .OrderBy(e => Vector3.Distance(e.transform.position, center))
                .Take(k_MaxTargets)
                .ToList();
        }

        static IEnumerator WaitUnscaled(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        // ------------------------------------------------------------------ 마법진

        Transform BuildSigil(Vector3 center, List<GameObject> cleanup)
        {
            var root = new GameObject("UltimateSigil").transform;
            root.position = center + Vector3.up * 0.06f;
            cleanup.Add(root.gameObject);

            Color gold = new Color(2f, 1.5f, 0.5f), cyan = new Color(0.6f, 1.6f, 2f);
            AddShape(root, MathFigure.Circle(Vector2.zero, 5.5f, 64), true, gold, 0.12f);
            AddShape(root, MathFigure.Circle(Vector2.zero, 4.3f, 64), true, cyan, 0.08f);
            Vector2[] penta = MathFigure.RegularPolygon(5, 4.3f, 90f);
            AddShape(root, new[] { penta[0], penta[2], penta[4], penta[1], penta[3] }, true, gold, 0.08f); // 오각별
            AddShape(root, MathFigure.RegularPolygon(6, 2.6f, 0f), true, cyan, 0.06f);

            string[] glyphs = { "π", "Σ", "∫", "θ", "√", "∞" };
            for (int i = 0; i < glyphs.Length; i++)
            {
                float a = i / (float)glyphs.Length * Mathf.PI * 2f;
                var t = m_Manager.CreateText(root, new Vector3(Mathf.Cos(a) * 6.6f, 0.02f, Mathf.Sin(a) * 6.6f), new Vector2(2f, 2f), glyphs[i], new Color(1f, 0.85f, 0.4f), 12f);
                t.transform.localRotation = Quaternion.Euler(90f, -a * Mathf.Rad2Deg + 90f, 0f);
            }

            // 가운데에서 솟는 빛기둥
            var pillar = m_Manager.CreateBlock(root, PrimitiveType.Cylinder, Vector3.up * 6f, new Vector3(0.6f, 6f, 0.6f), new Color(1.6f, 1.4f, 0.8f));
            pillar.name = "Pillar";
            return root;
        }

        void AddShape(Transform root, Vector2[] points, bool loop, Color color, float width)
        {
            LineRenderer line = m_Manager.CreateLine(root, color, width);
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                line.SetPosition(i, new Vector3(points[i].x, 0f, points[i].y));
            line.name = "Shape";
        }

        // u: 0 → 1 동안 선 굵기와 기둥이 점점 커진다
        static void DrawSigil(Transform sigil, float u)
        {
            foreach (LineRenderer l in sigil.GetComponentsInChildren<LineRenderer>())
                l.widthMultiplier = Mathf.Lerp(0f, 0.12f, Mathf.Clamp01(u * 2f));
            Transform pillar = sigil.Find("Pillar");
            if (pillar)
            {
                float w = Mathf.Lerp(0.1f, 1.2f, Mathf.Clamp01(u * 1.5f));
                pillar.localScale = new Vector3(w, 6f + 8f * u, w);
            }
        }

        List<LineRenderer> BuildSkyGrid(Vector3 center, List<GameObject> cleanup)
        {
            var lines = new List<LineRenderer>();
            float h = center.y + 26f;
            for (int i = -5; i <= 5; i++)
            {
                Color c = i == 0 ? new Color(2f, 0.6f, 0.6f) : new Color(0.4f, 1f, 1.6f);
                LineRenderer a = m_Manager.CreateLine(null, c, 0f);
                a.positionCount = 2;
                a.SetPositions(new[] { center + new Vector3(i * 8f, h, -40f), center + new Vector3(i * 8f, h, 40f) });
                Color c2 = i == 0 ? new Color(0.6f, 2f, 0.7f) : new Color(0.4f, 1f, 1.6f);
                LineRenderer b = m_Manager.CreateLine(null, c2, 0f);
                b.positionCount = 2;
                b.SetPositions(new[] { center + new Vector3(-40f, h, i * 8f), center + new Vector3(40f, h, i * 8f) });
                lines.Add(a);
                lines.Add(b);
            }

            // 사인 곡선 3개
            for (int k = 0; k < 3; k++)
            {
                LineRenderer wave = m_Manager.CreateLine(null, new Color(2f, 1.6f, 0.5f), 0f);
                wave.name = "Wave";
                wave.positionCount = 80;
                for (int i = 0; i < 80; i++)
                {
                    float x = Mathf.Lerp(-40f, 40f, i / 79f);
                    wave.SetPosition(i, center + new Vector3(x, h + 0.5f, (k - 1) * 14f + Mathf.Sin(x * 0.25f + k) * 5f));
                }
                lines.Add(wave);
            }

            foreach (LineRenderer l in lines) cleanup.Add(l.gameObject);
            return lines;
        }

        // ------------------------------------------------------------------ UI 도우미

        static RectTransform Bar(Transform canvas, bool top)
        {
            RectTransform bar = UIUtil.Image(canvas, top ? "TopBar" : "BottomBar", Color.black);
            bar.anchorMin = new Vector2(0f, top ? 1f : 0f);
            bar.anchorMax = new Vector2(1f, top ? 1f : 0f);
            bar.pivot = new Vector2(0.5f, top ? 1f : 0f);
            bar.sizeDelta = new Vector2(0f, 0f);
            return bar;
        }

        static void SetBars(RectTransform top, RectTransform bottom, float k)
        {
            float h = 130f * Mathf.SmoothStep(0f, 1f, k);
            top.sizeDelta = new Vector2(0f, h);
            bottom.sizeDelta = new Vector2(0f, h);
        }

        static void PlaceText(TextMeshProUGUI text, Vector2 pos, Vector2 size, Color color)
        {
            text.rectTransform.anchoredPosition = pos;
            text.rectTransform.sizeDelta = size;
            text.color = color;
            text.alpha = 0f;
        }
    }
}
