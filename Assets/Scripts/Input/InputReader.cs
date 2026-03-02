using System.Runtime.InteropServices;
using UnityEngine;

public static class InputReader
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static bool IsKeyPressed(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;
#else
    public static bool IsKeyPressed(int vKey) => false;
#endif

    public static bool IsKeyDown(int vKey, ref bool prevState)
    {
        bool current = IsKeyPressed(vKey);
        bool down = current && !prevState;
        prevState = current;
        return down;
    }

    public static Vector2 ReadMovementInput(bool isTeamA, bool isPlayer1)
    {
        float h = 0f;
        float v = 0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (isTeamA)
        {
            if (isPlayer1)
            {
                if (IsKeyPressed(0x57)) v += 1f; // W
                if (IsKeyPressed(0x53)) v -= 1f; // S
                if (IsKeyPressed(0x44)) h += 1f; // D
                if (IsKeyPressed(0x41)) h -= 1f; // A
            }
            else
            {
                if (IsKeyPressed(0x33)) v += 1f; // 3
                if (IsKeyPressed(0x32)) v -= 1f; // 2
                if (IsKeyPressed(0x34)) h += 1f; // 4
                if (IsKeyPressed(0x31)) h -= 1f; // 1
            }
        }
        else
        {
            if (isPlayer1)
            {
                if (IsKeyPressed(0x26)) v += 1f; // Up
                if (IsKeyPressed(0x28)) v -= 1f; // Down
                if (IsKeyPressed(0x27)) h += 1f; // Right
                if (IsKeyPressed(0x25)) h -= 1f; // Left
            }
            else
            {
                if (IsKeyPressed(0x39)) v += 1f; // 9
                if (IsKeyPressed(0x38)) v -= 1f; // 8
                if (IsKeyPressed(0x30)) h += 1f; // 0
                if (IsKeyPressed(0x37)) h -= 1f; // 7
            }
        }
#endif

        return new Vector2(h, v);
    }
}
