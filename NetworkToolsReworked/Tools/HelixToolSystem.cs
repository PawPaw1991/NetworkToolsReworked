using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Hover the open end of a road to preview a helix carrying on from it, or the ground to preview one
    /// centred there; click to lock it, set radius, turns, climb and direction in the panel, then click or
    /// Apply. A helix from a road end uses that road's type unless another type is copied; a free one
    /// needs a copied type. See <see cref="HelixEdit"/>.
    /// </summary>
    public partial class HelixToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private Entity m_TypePrefab;
        private bool m_PickingType;
        private bool m_Locked;
        private Entity m_LockedNode;
        private float3 m_LockedPoint;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.HelixTool";

        public ToolPhase Phase => m_PickingType ? ToolPhase.PickSource : m_Locked ? ToolPhase.Review : ToolPhase.PickStart;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>Name of the copied road type; empty when the helix uses the type of the road it continues.</summary>
        public string TypeName { get; private set; } = string.Empty;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        public void RequestPickType() => m_PickingType = true;

        public void UseRoadType()
        {
            m_TypePrefab = Entity.Null;
            TypeName = string.Empty;
            m_PickingType = false;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x4E11u);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_Locked = false;
            m_PickingType = false;
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_Locked = false;
            m_PickingType = false;
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.Terrain;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.Pathway | Layer.TrainTrack | Layer.TramTrack | Layer.SubwayTrack | Layer.Waterway | Layer.PublicTransportRoad;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
        }

        public override PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var applyRequested = m_ApplyRequested;
            var cancelRequested = m_CancelRequested;
            m_ApplyRequested = m_CancelRequested = false;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                if (m_PickingType)
                    m_PickingType = false;
                else if (m_Locked)
                    m_Locked = false;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_TypePrefab != Entity.Null && (!EntityManager.Exists(m_TypePrefab) || EntityManager.HasComponent<Deleted>(m_TypePrefab)))
                UseRoadType();
            if (m_Locked && m_LockedNode != Entity.Null && !OpenEnd(m_LockedNode, out _, out _))
                m_Locked = false;

            var click = applyAction.WasPressedThisFrame();
            var hasHit = GetRaycastResult(out Entity hitEntity, out RaycastHit hit);

            if (m_PickingType)
            {
                if (!hasHit || !IsPlainEdge(hitEntity))
                    return inputDeps;
                var prefab = EntityManager.GetComponentData<PrefabRef>(hitEntity).m_Prefab;
                m_Overlay.Edge(hitEntity, ToolOverlay.Hover);
                Summary = $"Click to build helixes as {PrefabName(prefab)}.";
                if (click)
                {
                    m_TypePrefab = prefab;
                    TypeName = PrefabName(prefab);
                    m_PickingType = false;
                }
                return inputDeps;
            }

            // Where the helix starts: a road's open end, or a point on the ground for its centre.
            Entity node;
            float3 point;
            if (m_Locked)
            {
                node = m_LockedNode;
                point = m_LockedPoint;
            }
            else
            {
                if (!hasHit)
                    return inputDeps;
                node = HoveredEnd(hitEntity, hit);
                point = hit.m_HitPosition;
            }

            var settings = Mod.Settings;
            var helix = new HelixParams
            {
                Radius = math.clamp(settings.HelixRadius, 8f, 500f),
                Turns = math.clamp(settings.HelixTurns, 0.25f, 12f),
                Climb = settings.HelixClimb,
                Clockwise = settings.HelixClockwise,
            };

            var terrain = m_TerrainSystem.GetHeightData();
            float3 centre;
            float startAngle;
            float startHeight;
            Entity prefabToUse;
            if (node != Entity.Null && OpenEnd(node, out var edge, out var direction))
            {
                var position = EntityManager.GetComponentData<Node>(node).m_Position;
                HelixEdit.Attach(position, direction, helix.Radius, helix.Clockwise, out centre, out startAngle);
                startHeight = position.y;
                prefabToUse = m_TypePrefab != Entity.Null ? m_TypePrefab : EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
                m_Overlay.Node(node, m_Locked ? ToolOverlay.End : ToolOverlay.Start);
            }
            else
            {
                node = Entity.Null;
                centre = point;
                startAngle = math.radians(settings.HelixStartAngle);
                var start = centre + new float3(math.cos(startAngle), 0f, math.sin(startAngle)) * helix.Radius;
                startHeight = TerrainUtils.SampleHeight(ref terrain, start) + settings.HelixStartHeight;
                prefabToUse = m_TypePrefab;
                if (prefabToUse == Entity.Null)
                {
                    m_Overlay.Point(centre, 4f, ToolOverlay.Invalid);
                    Summary = "Hover the open end of a road to carry on from it, or copy a road type below to place a helix anywhere.";
                    return inputDeps;
                }
                m_Overlay.Point(centre, 4f, m_Locked ? ToolOverlay.End : ToolOverlay.Start);
            }

            HelixEdit.Emit(EntityManager, m_ToolOutputBarrier.CreateCommandBuffer(), ref terrain, prefabToUse, centre, startAngle, startHeight, node, helix, m_Random.NextInt(), out var result);
            var limit = EntityManager.TryGetComponent(prefabToUse, out NetGeometryData geometry) && geometry.m_MaxSlopeSteepness > 0f ? geometry.m_MaxSlopeSteepness : 0.12f;
            var color = Grades.ColorFor(result.Grade, limit);
            foreach (var curve in result.Curves)
                m_Overlay.Bezier(curve, 3f, color);
            Summary = Describe(result, helix, limit, prefabToUse, node != Entity.Null);

            if (!m_Locked)
            {
                if (click)
                {
                    m_Locked = true;
                    m_LockedNode = node;
                    m_LockedPoint = point;
                }
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit();
                if (settings.DebugLogging)
                    Mod.Log.Info($"Helix applied: radius {helix.Radius}, {helix.Turns} turns, {helix.Climb} m per turn");
                m_Locked = false;
            }
            return inputDeps;
        }

        private string Describe(HelixResult r, HelixParams helix, float limit, Entity prefab, bool attached)
        {
            var direction = helix.Clockwise ? "clockwise" : "anticlockwise";
            var steep = math.abs(r.Grade) > limit ? $" (over the {limit * 100f:0}% limit: use a bigger radius or less climb)" : "";
            var ground = r.LowestAboveGround < -0.5f ? $". Part of it is {-r.LowestAboveGround:0.0} m below ground and will be a tunnel" : "";
            var from = attached ? "carrying on from the road, " : "";
            return $"{from}{PrefabName(prefab)}, radius {PathInfo.Distance(helix.Radius)}, {helix.Turns:0.##} turns {direction}, {PathInfo.Signed(r.TopHeight)} m in {PathInfo.Distance(r.Length)}, grade {math.abs(r.Grade) * 100f:0.0}%{steep}{ground}";
        }

        /// <summary>A node at the open end of a single plain road, with the direction leading out of it.</summary>
        private bool OpenEnd(Entity node, out Entity edge, out float3 direction)
        {
            edge = Entity.Null;
            direction = float3.zero;
            if (!ToolPicks.IsAlive(EntityManager, node) || !SlopeEdit.IsEditableNode(EntityManager, node))
                return false;
            var connected = EntityManager.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            if (connected.Length != 1 || !IsPlainEdge(connected[0].m_Edge))
                return false;
            edge = connected[0].m_Edge;
            var e = EntityManager.GetComponentData<Edge>(edge);
            var curve = EntityManager.GetComponentData<Curve>(edge).m_Bezier;
            direction = e.m_End == node ? MathUtils.EndTangent(curve) : -MathUtils.StartTangent(curve);
            return true;
        }

        private Entity HoveredEnd(Entity entity, RaycastHit hit)
        {
            if (EntityManager.HasComponent<Node>(entity))
                return entity;
            if (!IsPlainEdge(entity))
                return Entity.Null;
            var e = EntityManager.GetComponentData<Edge>(entity);
            var node = hit.m_CurvePosition < 0.5f ? e.m_Start : e.m_End;
            return OpenEnd(node, out _, out _) ? node : Entity.Null;
        }

        private bool IsPlainEdge(Entity entity)
        {
            return EntityManager.Exists(entity) && EntityManager.HasComponent<Edge>(entity) && !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Owner>(entity);
        }

        private string PrefabName(Entity prefab)
        {
            var asset = m_PrefabSystem.GetPrefab<PrefabBase>(prefab);
            return asset != null ? asset.name : "road";
        }
    }
}
