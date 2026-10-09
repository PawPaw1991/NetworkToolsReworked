using Colossal.Entities;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Click a start node, hover an end node to preview a new road between them, click it to lock the
    /// preview, then adjust and click again (or press Apply in the panel) to build it. The rotate keys
    /// turn the start direction in 15° steps. Right-click steps back one phase, or exits if nothing is
    /// picked. The new road uses the start node's road type.
    /// </summary>
    public partial class ConnectToolSystem : ToolBaseSystem, IPreviewTool
    {
        private const float kRotateStep = math.PI / 12f;
        private const float kGuideLength = 24f;

        private ToolOutputBarrier m_ToolOutputBarrier;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private ProxyAction m_RotateLeftAction;
        private ProxyAction m_RotateRightAction;
        private Entity m_StartNode;
        private Entity m_EndNode;
        private float m_RotationOffset;
        private int m_RotateRequest;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.ConnectTool";

        public ToolPhase Phase => m_StartNode == Entity.Null ? ToolPhase.PickStart : m_EndNode == Entity.Null ? ToolPhase.PickEnd : ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>Start direction turn in whole degrees; the rotate-left key adds, rotate-right subtracts.</summary>
        public int RotationDegrees => (int)math.round(math.degrees(m_RotationOffset));

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        /// <summary>Turns the start direction on the next update, in steps: +1 is one rotate-left press, -1 one rotate-right press.</summary>
        public void RequestRotate(int steps) => m_RotateRequest += steps;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0xC0EEu);
            m_RotateLeftAction = Mod.Settings.GetAction(nameof(Setting.ConnectRotateLeft));
            m_RotateRightAction = Mod.Settings.GetAction(nameof(Setting.ConnectRotateRight));
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            Reset();
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            m_RotateLeftAction.shouldBeEnabled = true;
            m_RotateRightAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            Reset();
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
            m_RotateLeftAction.shouldBeEnabled = false;
            m_RotateRightAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.Pathway | Layer.TrainTrack | Layer.TramTrack | Layer.SubwayTrack | Layer.Waterway | Layer.PublicTransportRoad;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
        }

        public override PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var applyRequested = m_ApplyRequested;
            var cancelRequested = m_CancelRequested;
            var rotateSteps = m_RotateRequest;
            m_ApplyRequested = m_CancelRequested = false;
            m_RotateRequest = 0;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                if (m_EndNode != Entity.Null)
                    m_EndNode = Entity.Null;
                else if (m_StartNode != Entity.Null)
                    Reset();
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            if (m_RotateLeftAction.WasPerformedThisFrame())
                rotateSteps++;
            if (m_RotateRightAction.WasPerformedThisFrame())
                rotateSteps--;
            m_RotationOffset += rotateSteps * kRotateStep;

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_StartNode != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_StartNode))
                Reset();
            if (m_EndNode != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_EndNode))
                m_EndNode = Entity.Null;

            var click = applyAction.WasPressedThisFrame();
            var hasHit = GetRaycastResult(out Entity hitEntity, out RaycastHit hit);
            var hovered = hasHit ? HoveredNode(hitEntity, hit) : Entity.Null;

            if (m_StartNode == Entity.Null)
            {
                if (hovered != Entity.Null)
                {
                    var usable = ConnectEdit.PrefabFor(EntityManager, hovered) != Entity.Null;
                    m_Overlay.Node(hovered, usable ? ToolOverlay.Hover : ToolOverlay.Invalid);
                    if (click && usable)
                        m_StartNode = hovered;
                }
                return inputDeps;
            }

            var locked = m_EndNode != Entity.Null;
            var end = locked ? m_EndNode : hovered;
            var start = EntityManager.GetComponentData<Node>(m_StartNode).m_Position;

            // The start direction guide follows the cursor until an end node is found.
            var target = end != Entity.Null ? EntityManager.GetComponentData<Node>(end).m_Position : hasHit ? hit.m_HitPosition : start;
            var direction = math.rotate(quaternion.RotateY(m_RotationOffset), ConnectEdit.DefaultDirection(EntityManager, m_StartNode, target));
            m_Overlay.Node(m_StartNode, ToolOverlay.Start);
            m_Overlay.Line(start, start + math.normalizesafe(new float3(direction.x, 0f, direction.z)) * kGuideLength, 1.5f, ToolOverlay.Start);

            if (end == Entity.Null || end == m_StartNode)
                return inputDeps;

            var ecb = m_ToolOutputBarrier.CreateCommandBuffer();
            if (!ConnectEdit.Emit(EntityManager, ecb, m_StartNode, end, direction, Mod.Settings.ConnectMode, m_Random.NextInt()))
            {
                m_Overlay.Node(end, ToolOverlay.Invalid);
                Summary = "These nodes can't be connected.";
                return inputDeps;
            }

            m_Overlay.Node(end, locked ? ToolOverlay.End : ToolOverlay.Hover);
            Summary = Describe(start, target);

            if (!locked)
            {
                if (click)
                    m_EndNode = end;
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit(Summary);
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Connect applied from {m_StartNode} to {end}");
                Reset();
            }

            return inputDeps;
        }

        private string Describe(float3 start, float3 end)
        {
            var distance = math.distance(start.xz, end.xz);
            var rise = end.y - start.y;
            var grade = distance > 0.01f ? rise / distance * 100f : 0f;
            var turn = RotationDegrees == 0 ? "" : $", start turned {RotationDegrees}°";
            return $"Distance {PathInfo.Distance(distance)}, height {PathInfo.Signed(rise)} m, grade {PathInfo.Signed(grade)}%{turn}";
        }

        private void Reset()
        {
            m_StartNode = Entity.Null;
            m_EndNode = Entity.Null;
            m_RotationOffset = 0f;
            Summary = string.Empty;
        }

        private Entity HoveredNode(Entity entity, RaycastHit hit)
        {
            if (EntityManager.HasComponent<Node>(entity))
                return EntityManager.HasComponent<Owner>(entity) ? Entity.Null : entity;
            if (EntityManager.HasComponent<Owner>(entity) || !EntityManager.TryGetComponent(entity, out Edge edge))
                return Entity.Null;
            return hit.m_CurvePosition < 0.5f ? edge.m_Start : edge.m_End;
        }
    }
}
