using System.IO;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

// 수학 무기마다 겉모습을 다르게 만든다.
// 1) 본체 머티리얼을 무기 색으로 물들이고  2) 수식을 떠올리게 하는 부착물(고리, 총열, 꽃잎, 레일 등)을 총구 주변에 붙인다.
public static class WeaponLooks
{
    public static void Decorate(WeaponController controller, string id, Color color, Material lit, Material unlit, string materialFolder)
    {
        Transform root = controller.WeaponRoot ? controller.WeaponRoot.transform : controller.transform;
        int layer = root.gameObject.layer;

        Tint(root, id, color, materialFolder);

        // 무기 크기를 기준으로 부착물 크기를 맞춘다
        Bounds bounds = LocalBounds(root);
        float s = Mathf.Max(bounds.size.z, 0.2f);
        Vector3 m = controller.WeaponMuzzle
            ? root.InverseTransformPoint(controller.WeaponMuzzle.position)
            : new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);

        Transform old = root.Find("MathAttachments");
        if (old) Object.DestroyImmediate(old.gameObject);
        var parts = new GameObject("MathAttachments").transform;
        parts.SetParent(root, false);

        Material glow = SavedMaterial(unlit, $"{materialFolder}/{id}_Glow.mat", color, 0f);
        Material metal = SavedMaterial(lit, $"{materialFolder}/{id}_Metal.mat", Color.Lerp(color, new Color(0.15f, 0.15f, 0.18f), 0.6f), 0.8f);
        Vector3 fwd = Vector3.forward;

        switch (id)
        {
            case "Sine": // 총열을 감싼 빛나는 고리 3개
                for (int i = 0; i < 3; i++)
                    Part(parts, PrimitiveType.Cylinder, m - fwd * s * (0.05f + 0.08f * i), new Vector3(0.16f, 0.01f, 0.16f) * s, new Vector3(90f, 0f, 0f), glow);
                break;

            case "Zigzag": // 기울어진 탄창 + 지그재그 핀
                Part(parts, PrimitiveType.Cube, m + new Vector3(0f, -0.13f, -0.35f) * s, new Vector3(0.05f, 0.22f, 0.08f) * s, new Vector3(-15f, 0f, 0f), metal);
                for (int i = 0; i < 4; i++)
                    Part(parts, PrimitiveType.Cube, m + new Vector3(0f, 0.06f, -0.08f - 0.07f * i) * s, new Vector3(0.015f, 0.05f, 0.06f) * s,
                        new Vector3(i % 2 == 0 ? 35f : -35f, 0f, 0f), glow);
                break;

            case "Helix": // 총구 앞에서 회전하는 3연장 총열
            {
                var pivot = new GameObject("Spinner").transform;
                pivot.SetParent(parts, false);
                pivot.localPosition = m + fwd * s * 0.02f;
                pivot.localRotation = Quaternion.Euler(90f, 0f, 0f); // 회전축(local up)을 총열 방향으로
                pivot.gameObject.AddComponent<ConstantRotation>().RotatingSpeed = 540f;
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f * Mathf.Deg2Rad;
                    Part(pivot, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.05f * s,
                        new Vector3(0.035f, 0.1f, 0.035f) * s, Vector3.zero, i == 0 ? glow : metal);
                }
                break;
            }

            case "Cone": // 넓게 벌어진 원형 총구
                Part(parts, PrimitiveType.Cylinder, m + fwd * s * 0.02f, new Vector3(0.22f, 0.02f, 0.22f) * s, new Vector3(90f, 0f, 0f), glow);
                Part(parts, PrimitiveType.Cylinder, m + fwd * s * 0.03f, new Vector3(0.15f, 0.02f, 0.15f) * s, new Vector3(90f, 0f, 0f), metal);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f;
                    Vector3 offset = Quaternion.AngleAxis(a, fwd) * Vector3.up * 0.09f * s;
                    Part(parts, PrimitiveType.Cube, m + offset - fwd * s * 0.03f, new Vector3(0.015f, 0.03f, 0.1f) * s, new Vector3(0f, 0f, a), glow);
                }
                break;

            case "Damped": // 긴 총열 + 조준경
                Part(parts, PrimitiveType.Cylinder, m + fwd * s * 0.22f, new Vector3(0.035f, 0.22f, 0.035f) * s, new Vector3(90f, 0f, 0f), metal);
                Part(parts, PrimitiveType.Cylinder, m + fwd * s * 0.44f, new Vector3(0.05f, 0.02f, 0.05f) * s, new Vector3(90f, 0f, 0f), glow);
                Part(parts, PrimitiveType.Cylinder, new Vector3(m.x, bounds.max.y + 0.05f * s, m.z - 0.3f * s), new Vector3(0.07f, 0.13f, 0.07f) * s, new Vector3(90f, 0f, 0f), metal);
                Part(parts, PrimitiveType.Cylinder, new Vector3(m.x, bounds.max.y + 0.05f * s, m.z - 0.17f * s), new Vector3(0.06f, 0.005f, 0.06f) * s, new Vector3(90f, 0f, 0f), glow);
                break;

            case "Rose": // 총구의 꽃잎 5장 (r = cos 3θ 의 꽃잎 느낌)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f;
                    Vector3 offset = Quaternion.AngleAxis(a, fwd) * Vector3.up * 0.08f * s;
                    Part(parts, PrimitiveType.Cube, m + offset + fwd * s * 0.02f, new Vector3(0.05f, 0.12f, 0.015f) * s, new Vector3(0f, 0f, a), glow);
                }
                Part(parts, PrimitiveType.Cylinder, m, new Vector3(0.09f, 0.03f, 0.09f) * s, new Vector3(90f, 0f, 0f), metal);
                break;

            case "Exponential": // 위아래 레일 + 간격이 2배씩 늘어나는 코일 (2⁰, 2¹, 2², 2³)
                Part(parts, PrimitiveType.Cube, m + new Vector3(0f, 0.06f, 0.1f) * s, new Vector3(0.02f, 0.02f, 0.5f) * s, Vector3.zero, glow);
                Part(parts, PrimitiveType.Cube, m + new Vector3(0f, -0.06f, 0.1f) * s, new Vector3(0.02f, 0.02f, 0.5f) * s, Vector3.zero, glow);
                for (int i = 0; i < 4; i++)
                    Part(parts, PrimitiveType.Cylinder, m - fwd * s * (0.025f * Mathf.Pow(2f, i)), new Vector3(0.13f, 0.008f, 0.13f) * s, new Vector3(90f, 0f, 0f), metal);
                break;

            default: // 포물선 런처: 꼬리 날개 3장
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f;
                    Vector3 offset = Quaternion.AngleAxis(a, fwd) * Vector3.up * 0.08f * s;
                    Part(parts, PrimitiveType.Cube, m + offset - fwd * s * 0.05f, new Vector3(0.01f, 0.08f, 0.14f) * s, new Vector3(0f, 0f, a), glow);
                }
                break;
        }

        foreach (Transform t in parts.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    static void Tint(Transform root, string id, Color color, string folder)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material source = mats[i];
                if (!source || source.name.StartsWith(id + "_")) continue;
                // 투명(유리·화면 등) 재질과 색 속성이 없는 재질은 건드리지 않는다
                if (source.renderQueue >= 2450 || !source.HasProperty("_BaseColor")) continue;
                Color baseColor = source.GetColor("_BaseColor");
                mats[i] = SavedMaterial(source, $"{folder}/{id}_{Sanitize(source.name)}.mat", Color.Lerp(baseColor, color, 0.3f), -1f);
            }

            r.sharedMaterials = mats;
        }
    }

    static Material SavedMaterial(Material source, string path, Color color, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (smoothness >= 0f && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Vector3 euler, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Bounds LocalBounds(Transform root)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Bounds wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = root.InverseTransformPoint(corner);
                if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
                else bounds.Encapsulate(local);
            }
        }

        return bounds;
    }

    static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(" (Instance)", "");
    }
}
