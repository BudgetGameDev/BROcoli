using System.Collections;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseStreamingRevisionDuringNavmesh(DungeonManager manager)
        {
            Drain((IEnumerator)InvokeHierarchy(manager, "StreamRooms"));
            SetHierarchyField(manager, "navMeshDirty", true);
            var streaming = (IEnumerator)InvokeHierarchy(manager, "StreamRooms");
            Assert.That(streaming.MoveNext(), Is.True);
            SetHierarchyField(
                manager,
                "streamingRevision",
                GetHierarchyField<int>(manager, "streamingRevision") + 1
            );
            Drain(streaming);
            InvokeHierarchy(manager, "RequestRoomStreaming");
        }
    }
}
