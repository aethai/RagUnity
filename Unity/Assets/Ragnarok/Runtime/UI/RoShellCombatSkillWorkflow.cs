using System;
using System.Threading.Tasks;
using Ragnarok.Gameplay;

namespace Ragnarok.UI {
    internal sealed class RoShellRuntimeSkillCommandWorkflow {
        private readonly Func<int, int, int, Task> useSkillOnTarget;
        private readonly Func<int, int, int, int, Task> useSkillOnGround;

        public RoShellRuntimeSkillCommandWorkflow(
            Func<int, int, int, Task> useSkillOnTarget,
            Func<int, int, int, int, Task> useSkillOnGround) {
            this.useSkillOnTarget = useSkillOnTarget;
            this.useSkillOnGround = useSkillOnGround;
        }

        public bool CanUseTargetSkill => useSkillOnTarget != null;
        public bool CanUseGroundSkill => useSkillOnGround != null;

        public Task BeginUseSkillOnTarget(int skillId, int skillLevel, int targetAid) {
            if (useSkillOnTarget == null) {
                throw new InvalidOperationException("Runtime target skill command executor is not configured.");
            }

            return useSkillOnTarget(skillId, skillLevel, targetAid);
        }

        public Task BeginUseSkillOnGround(int skillId, int skillLevel, int x, int y) {
            if (useSkillOnGround == null) {
                throw new InvalidOperationException("Runtime ground skill command executor is not configured.");
            }

            return useSkillOnGround(skillId, skillLevel, x, y);
        }
    }

    internal sealed class RoShellCombatSkillWorkflow {
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly RoShellRuntimeSkillCommandWorkflow runtimeSkillWorkflow;
        private readonly Action<int, int, int> targetSkillSent;

        public RoShellCombatSkillWorkflow(
            RoShellGameplayPacketWorkflow gameplayPacketWorkflow,
            Func<int, int, int, Task> runtimeUseSkillOnTarget = null,
            Func<int, int, int, int, Task> runtimeUseSkillOnGround = null,
            Action<int, int, int> targetSkillSent = null) {
            this.gameplayPacketWorkflow = gameplayPacketWorkflow ?? throw new ArgumentNullException(nameof(gameplayPacketWorkflow));
            this.targetSkillSent = targetSkillSent;
            if (runtimeUseSkillOnTarget != null || runtimeUseSkillOnGround != null) {
                runtimeSkillWorkflow = new RoShellRuntimeSkillCommandWorkflow(
                    runtimeUseSkillOnTarget,
                    runtimeUseSkillOnGround);
            }
        }

        public Task BeginPickupGroundItem(int itemAid) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildPickupGroundItem(itemAid),
                $"picking up item {itemAid}",
                $"sent pickup request for item {itemAid}",
                new RoLocalActionLockRequest(
                    RoGameplayActionLockKind.Pickup,
                    RoGameplayActionLockResolver.DefaultPickupFallbackMilliseconds,
                    $"pickup item {itemAid}"));
        }

        public Task BeginAttackTarget(int targetAid, bool repeatAttack) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildAttackTarget(targetAid, repeatAttack),
                $"attacking target {targetAid}",
                $"sent attack request for target {targetAid}",
                new RoLocalActionLockRequest(
                    RoGameplayActionLockKind.Attack,
                    RoGameplayActionLockResolver.DefaultAttackFallbackMilliseconds,
                    $"attack target {targetAid}"));
        }

        public Task BeginUseSkillOnTarget(int skillId, int skillLevel, int targetAid) {
            if (runtimeSkillWorkflow == null || !runtimeSkillWorkflow.CanUseTargetSkill) {
                throw new InvalidOperationException("Runtime skill command executor is not configured. Use the explicit legacy skill fallback for the old live shell path.");
            }

            return runtimeSkillWorkflow.BeginUseSkillOnTarget(skillId, skillLevel, targetAid);
        }

        public Task BeginLegacyUseSkillOnTarget(int skillId, int skillLevel, int targetAid) {
            return gameplayPacketWorkflow.Begin(
                snapshot => new RoShellPreparedGameplayPacket(
                    RoShellGameplayCommands.BuildLegacyUseSkillOnTarget(snapshot, skillId, skillLevel, targetAid),
                    $"casting skill {skillId} on target {targetAid}",
                    $"sent skill {skillId} request for target {targetAid}"),
                afterPrepareLocked: null,
                afterSentLocked: (_, _, _) => targetSkillSent?.Invoke(skillId, skillLevel, targetAid),
                actionLockRequest: new RoLocalActionLockRequest(
                    RoGameplayActionLockKind.Skill,
                    RoGameplayActionLockResolver.DefaultSkillFallbackMilliseconds,
                    $"skill {skillId} target {targetAid}"));
        }

        public Task BeginUseSkillOnGround(int skillId, int skillLevel, int x, int y) {
            if (runtimeSkillWorkflow == null || !runtimeSkillWorkflow.CanUseGroundSkill) {
                throw new InvalidOperationException("Runtime skill command executor is not configured. Use the explicit legacy skill fallback for the old live shell path.");
            }

            return runtimeSkillWorkflow.BeginUseSkillOnGround(skillId, skillLevel, x, y);
        }

        public Task BeginLegacyUseSkillOnGround(int skillId, int skillLevel, int x, int y) {
            return gameplayPacketWorkflow.Begin(
                snapshot => RoShellGameplayCommands.BuildLegacyUseSkillOnGround(snapshot, skillId, skillLevel, x, y),
                $"casting skill {skillId} at {x},{y}",
                $"sent skill {skillId} request at {x},{y}",
                new RoLocalActionLockRequest(
                    RoGameplayActionLockKind.Skill,
                    RoGameplayActionLockResolver.DefaultSkillFallbackMilliseconds,
                    $"skill {skillId} ground {x},{y}"));
        }
    }
}
