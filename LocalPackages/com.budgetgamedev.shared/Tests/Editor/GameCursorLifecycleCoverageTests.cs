using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class GameCursorLifecycleCoverageTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void ResetCursor()
        {
            foreach (
                GameCursor cursor in Object.FindObjectsByType<GameCursor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
                Object.DestroyImmediate(cursor.gameObject);
            FieldInfo pointer = typeof(GameCursor).GetField(
                "tintedPointer",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            if (pointer.GetValue(null) is Texture2D texture)
                Object.DestroyImmediate(texture);
            pointer.SetValue(null, null);
            InvokeStatic("ResetStaticState");
        }

        [Test]
        public void CursorSingletonAppliesBlankAndMissingPointerArtSafely()
        {
            GameCursor cursor = CreateCursor();

            Assert.That(GameCursor.IsPointerShown, Is.TypeOf<bool>());
            typeof(GameCursor)
                .GetField("tintedPointer", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, new Texture2D(1, 1));
            LogAssert.Expect(LogType.Error, new Regex("^Destroy may not be called from edit mode"));
            GameCursor.SetArt(default);
            Assert.That(GameCursor.Art.IsEmpty, Is.True);

            LogAssert.Expect(LogType.Warning, "GameCursor: no pointer texture at Missing/Pointer");
            GameCursor.SetArt(new GameCursor.PointerArt("Missing/Pointer", Color.green));
            Assert.That(GameCursor.Art.PointerResource, Is.EqualTo("Missing/Pointer"));
        }

        [Test]
        public void CursorTracksOnlyRealPointerMovement()
        {
            GameCursor cursor = CreateCursor();
            MethodInfo note = typeof(GameCursor).GetMethod("NotePointerPosition", InstancePrivate);

            note.Invoke(cursor, new object[] { new Vector2(10f, 10f) });
            note.Invoke(cursor, new object[] { new Vector2(10.1f, 10.1f) });
            note.Invoke(cursor, new object[] { new Vector2(100f, 100f) });

            Assert.That(
                typeof(GameCursor)
                    .GetField("lastPointerPosition", InstancePrivate)
                    .GetValue(cursor),
                Is.EqualTo(new Vector2(100f, 100f))
            );
        }

        [Test]
        public void EmptyPointerStateCanBeUpdatedAndDestroyed()
        {
            GameCursor cursor = CreateCursor();
            typeof(GameCursor).GetMethod("Update", InstancePrivate).Invoke(cursor, null);
            Object.DestroyImmediate(cursor.gameObject);

            Assert.That(GameCursor.IsPointerShown, Is.True);
            Assert.That(GameCursor.TryReadPointerPosition(null, out Vector2 position), Is.False);
            Assert.That(position, Is.EqualTo(Vector2.zero));
            cursor = CreateCursor();
            cursor.UpdatePointer(null);
        }

        [Test]
        public void ExistingAndDuplicateCursorLifecyclePathsRestoreThePointer()
        {
            GameCursor cursor = CreateCursor();
            Assert.That(GameCursor.EnsurePresent(), Is.SameAs(cursor));

            var duplicateHost = new GameObject("Duplicate cursor");
            duplicateHost.SetActive(false);
            var duplicate = duplicateHost.AddComponent<GameCursor>();
            LogAssert.Expect(LogType.Error, new Regex("^Destroy may not be called from edit mode"));
            typeof(GameCursor).GetMethod("Awake", InstancePrivate).Invoke(duplicate, null);
            typeof(GameCursor).GetMethod("OnDestroy", InstancePrivate).Invoke(duplicate, null);

            Object.DestroyImmediate(duplicateHost);
            Object.DestroyImmediate(cursor.gameObject);
            Assert.That(GameCursor.IsPointerShown, Is.True);
        }

        private static void InvokeStatic(string name) =>
            typeof(GameCursor)
                .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

        private static GameCursor CreateCursor()
        {
            var host = new GameObject("Cursor lifecycle coverage");
            host.SetActive(false);
            var cursor = host.AddComponent<GameCursor>();
            typeof(GameCursor).GetMethod("Awake", InstancePrivate).Invoke(cursor, null);
            return cursor;
        }
    }
}
