// ---------------------------------------------------------------------------
// Hand-written stub of the UnityEngine surface used by MinecraftInRepo.
// Compiled into the stub Assembly-CSharp.dll only when building without the
// real game. Bodies are minimal; only signatures matter here.
// Mirrors Unity 2022.3 (the Unity version R.E.P.O. runs on).
// ---------------------------------------------------------------------------

using System;
using System.Collections;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }

        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
        public static void DontDestroyOnLoad(Object target) { }

        public static T FindObjectOfType<T>() where T : Object { return null; }
        public static T FindObjectOfType<T>(bool includeInactive) where T : Object { return null; }
        public static T[] FindObjectsOfType<T>() where T : Object { return new T[0]; }
        public static T[] FindObjectsOfType<T>(bool includeInactive) where T : Object { return new T[0]; }

        public static implicit operator bool(Object exists) { return exists != null; }
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);

        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-6f ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }

        public override string ToString() { return string.Format("({0:F2}, {1:F2}, {2:F2})", x, y, z); }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; private set; }
        public Transform transform { get; private set; }

        public T GetComponent<T>() where T : Component { return null; }
        public T GetComponentInChildren<T>() where T : Component { return null; }
        public T[] GetComponentsInChildren<T>() where T : Component { return new T[0]; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component { return new T[0]; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class Coroutine : YieldInstruction { }

    public class YieldInstruction { }

    public class WaitForSeconds : YieldInstruction
    {
        public WaitForSeconds(float seconds) { }
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) { return new Coroutine(); }
        public void StopCoroutine(Coroutine routine) { }
        public void StopAllCoroutines() { }
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }

        public Transform transform { get; private set; }
        public bool activeSelf { get; private set; }

        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public T GetComponent<T>() where T : Component { return null; }
        public T GetComponentInChildren<T>() where T : Component { return null; }
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 up { get; set; }
        public Vector3 right { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Transform parent { get; set; }

        public Transform Find(string n) { return null; }
        public void SetParent(Transform p) { }
        public void LookAt(Vector3 worldPosition) { }
    }

    public class Camera : Behaviour
    {
        public static Camera main { get; private set; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
    }

    public class Rigidbody : Component
    {
        public Vector3 velocity { get; set; }
        public float mass { get; set; }
        public bool isKinematic { get; set; }

        public void AddForce(Vector3 force) { }
        public void AddForce(Vector3 force, ForceMode mode) { }
        public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius) { }
        public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier) { }
        public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier, ForceMode mode) { }
    }

    public enum ForceMode
    {
        Force = 0,
        Acceleration = 1,
        Impulse = 2,
        VelocityChange = 5,
    }

    public class Collider : Component
    {
        public bool enabled { get; set; }
    }

    public class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
    }

    public class Time
    {
        public static float time => 0f;
        public static float deltaTime => 0.016f;
        public static float unscaledTime => 0f;
        public static float unscaledDeltaTime => 0.016f;
        public static float realtimeSinceStartup => 0f;
    }

    public class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
        public static void LogException(Exception exception) { }
    }

    public class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int value) { return Math.Abs(value); }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Clamp(float value, float min, float max) { return value < min ? min : (value > max ? max : value); }
        public static int Clamp(int value, int min, int max) { return value < min ? min : (value > max ? max : value); }
        public static float Clamp01(float value) { return value < 0f ? 0f : (value > 1f ? 1f : value); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static float Round(float f) { return (float)Math.Round(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Asin(float f) { return (float)Math.Asin(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static bool Approximately(float a, float b) { return Math.Abs(a - b) < 1e-6f; }
        public static float Repeat(float t, float length) { return Clamp(t - (float)Math.Floor(t / length) * length, 0f, length); }
        public static float DeltaAngle(float current, float target) { return Repeat(target - current + 180f, 360f) - 180f; }
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color yellow => new Color(1f, 0.92f, 0.016f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
    }

    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    public enum TextureFormat
    {
        RGBA32 = 4,
        BGRA32 = 14,
    }

    public class Texture : Object
    {
        public int width { get; protected set; }
        public int height { get; protected set; }
    }

    public class Texture2D : Texture
    {
        public Texture2D(int width, int height) { this.width = width; this.height = height; }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { this.width = width; this.height = height; }

        public void LoadRawTextureData(byte[] data) { }
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
    }

    public enum TextAnchor
    {
        UpperLeft = 0,
        UpperCenter = 1,
        UpperRight = 2,
        MiddleLeft = 3,
        MiddleCenter = 4,
        MiddleRight = 5,
        LowerLeft = 6,
        LowerCenter = 7,
        LowerRight = 8,
    }

    public class GUIStyleState
    {
        // Real Unity spells this "textColor" - the stubs must match exactly.
        public Color textColor { get; set; }
        public Texture2D background { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { normal = new GUIStyleState(); }
        public GUIStyleState normal { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
    }

    public class GUI
    {
        public static int depth { get; set; }
        public static Color color { get; set; }

        public static void DrawTexture(Rect screenRect, Texture texture) { }
        public static void Label(Rect screenRect, string text) { }
        public static void Label(Rect screenRect, string text, GUIStyle style) { }
    }

    public enum KeyCode
    {
        None = 0,
        F5 = 286,
        F6 = 287,
        F7 = 288,
        F8 = 289,
        F9 = 290,
        F10 = 291,
    }

    public class Input
    {
        public static bool GetKey(KeyCode key) { return false; }
        public static bool GetKeyDown(KeyCode key) { return false; }
        public static bool GetKeyUp(KeyCode key) { return false; }
    }

    public class Application
    {
        public static string dataPath => "";
        public static string persistentDataPath => "";
    }
}
