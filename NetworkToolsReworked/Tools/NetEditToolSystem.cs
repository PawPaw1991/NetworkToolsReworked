using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Base for every tool that writes definitions. Handles the definition lifecycle the way the game's
    /// own tools do:
    /// <list type="bullet">
    /// <item>Last frame's definitions are destroyed at the start of every update, so they don't pile up
    /// (or end up in the save).</item>
    /// <item>Definitions go into <see cref="DefinitionBuffer"/> and are created at the end of the update,
    /// unless the tool applies that frame. Applying keeps the preview the player was looking at (built
    /// from last frame's definitions); new definitions on the apply frame would be built after it and
    /// could leave a piece out.</item>
    /// </list>
    /// Tools override <see cref="OnToolUpdate"/> instead of OnUpdate.
    /// </summary>
    public abstract partial class NetEditToolSystem : ToolBaseSystem
    {
        private EntityQuery m_OwnDefinitionQuery;
        private ToolOutputBarrier m_DefinitionBarrier;
        private EntityCommandBuffer m_Buffer;
        private bool m_HasBuffer;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OwnDefinitionQuery = GetDefinitionQuery();
            m_DefinitionBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
        }

        protected override void OnStopRunning()
        {
            DiscardBuffer();
            EntityManager.DestroyEntity(m_OwnDefinitionQuery);
            base.OnStopRunning();
        }

        /// <summary>Command buffer for this frame's definitions.</summary>
        protected EntityCommandBuffer DefinitionBuffer()
        {
            if (!m_HasBuffer)
            {
                m_Buffer = new EntityCommandBuffer(Allocator.TempJob);
                m_HasBuffer = true;
            }
            return m_Buffer;
        }

        protected abstract JobHandle OnToolUpdate(JobHandle inputDeps);

        protected sealed override JobHandle OnUpdate(JobHandle inputDeps)
        {
            inputDeps = DestroyDefinitions(m_OwnDefinitionQuery, m_DefinitionBarrier, inputDeps);
            try
            {
                inputDeps = OnToolUpdate(inputDeps);
                if (m_HasBuffer && applyMode != ApplyMode.Apply)
                    m_Buffer.Playback(EntityManager);
            }
            finally
            {
                DiscardBuffer();
            }
            return inputDeps;
        }

        private void DiscardBuffer()
        {
            if (!m_HasBuffer)
                return;
            m_Buffer.Dispose();
            m_HasBuffer = false;
        }
    }
}
