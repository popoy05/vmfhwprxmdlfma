using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // 화면 터치 버튼(모바일 조작) 같은 '가상 입력'을 키보드·마우스 입력과 합치기 위한 저장소.
    // UI 이벤트는 프레임 중간에 들어오므로, 눌림 신호는 다음 프레임 맨 앞(Pump)에서 한꺼번에 '이번 프레임 눌림'으로 바뀐다.
    public static class VirtualInput
    {
        public static bool Active;              // 모바일 조작이 켜져 있는지
        public static Vector2 Move;             // 왼쪽 조이스틱 (-1 ~ 1)
        public static Vector2 Look;             // 이번 프레임 시선 이동량 (픽셀)
        public static float LookScale = 0.0016f; // 터치 픽셀 → 시선 입력 배율 (마우스 바인딩의 0.001과 같은 체계)

        static Vector2 s_PendingLook;
        static readonly HashSet<string> s_Pending = new HashSet<string>();
        static readonly HashSet<string> s_ThisFrame = new HashSet<string>();
        static readonly HashSet<string> s_Held = new HashSet<string>();
        static int s_PumpedFrame = -1;

        public static void AddLook(Vector2 delta) => s_PendingLook += delta;
        public static void Press(string id) { s_Pending.Add(id); s_Held.Add(id); }
        public static void Release(string id) => s_Held.Remove(id);
        public static void Tap(string id) => s_Pending.Add(id);

        public static bool WasPressed(string id) { Pump(); return s_ThisFrame.Contains(id); }
        public static bool IsHeld(string id) => s_Held.Contains(id);

        // 프레임마다 한 번만 실행된다 (먼저 읽는 쪽이 실행)
        public static void Pump()
        {
            if (s_PumpedFrame == Time.frameCount) return;
            s_PumpedFrame = Time.frameCount;
            s_ThisFrame.Clear();
            foreach (string id in s_Pending) s_ThisFrame.Add(id);
            s_Pending.Clear();
            Look = s_PendingLook;
            s_PendingLook = Vector2.zero;
        }

        public static Vector2 LookThisFrame { get { Pump(); return Look; } }

        public static void ResetAll()
        {
            Move = Vector2.zero;
            Look = s_PendingLook = Vector2.zero;
            s_Pending.Clear();
            s_ThisFrame.Clear();
            s_Held.Clear();
        }
    }
}
