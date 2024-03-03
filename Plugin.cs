using BepInEx;
using BepInEx.Configuration;
using System;
using System.Security;
using System.Security.Permissions;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static RoR2ExtinguishInWater.StaticMethods;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

#pragma warning restore CS0618 // Type or member is obsolete


namespace RoR2ExtinguishInWater
{
    [BepInPlugin("com.DestroyedClone.ExtinguishingWater", "Extinguishing Water", "0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static SurfaceDef waterSD = Addressables.LoadAssetAsync<SurfaceDef>("RoR2/Base/Common/sdWater.asset").WaitForCompletion();
        public static ConfigEntry<bool> AllowPlayers { get; set; }
        public static ConfigEntry<bool> AllowAllies { get; set; }
        public static ConfigEntry<bool> AllowEnemies { get; set; }
        public static ConfigEntry<bool> PreventUnderwaterIgnition { get; set; }
        public static ConfigEntry<bool> Commands { get; set; }

        public void Awake()
        {
            AllowPlayers = Config.Bind("Filter", "Allow Players", true, "Allow players, regardless of team, to get extinguished.");
            AllowAllies = Config.Bind("Filter", "Allow Allies", true, "Allow allies, excluding players, to get extinguished.");
            AllowEnemies = Config.Bind("Filter", "Allow Enemies", true, "Allow enemies to get extinguished.");
            Commands = Config.Bind("Other", "Enable Burn Commands", true, "Enable commands to set yourself on fire. More for debugging than anything.");
            PreventUnderwaterIgnition = Config.Bind("Filter", "Prevent Underwater Ignition", true, "Prevent attacks from igniting if the victim is submerged.");

            if (Commands.Value)
                R2API.Utils.CommandHelper.AddToConsoleWhenReady();

            if (AllowPlayers.Value || AllowAllies.Value || AllowEnemies.Value)
            {
                On.RoR2.GlobalEventManager.OnCharacterHitGround += ExtinguishInWaterJump;
                On.RoR2.FootstepHandler.Footstep_string_GameObject += ExtinguishFootstep;
                On.RoR2.DotController.InflictDot_refInflictDotInfo += ExtinguishInflict;
            }
        }

        private void ExtinguishInflict(On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo inflictDotInfo)
        {
            if (inflictDotInfo.victimObject)
                if (CheckForWater(inflictDotInfo.victimObject.transform.position))
                {
                    if (inflictDotInfo.dotIndex == DotController.DotIndex.PercentBurn || inflictDotInfo.dotIndex == DotController.DotIndex.Burn)
                    {
                        //Chat.AddMessage("prevented underwater ignition");
                        inflictDotInfo.duration = 0f;
                    }
                }
                else
                {
                    var component = inflictDotInfo.victimObject.AddComponent<Extinguisher>();
                    component.characterBody = inflictDotInfo.victimObject.GetComponent<CharacterBody>();
                }
            orig(ref inflictDotInfo);
        }

        private void ExtinguishFootstep(On.RoR2.FootstepHandler.orig_Footstep_string_GameObject orig, FootstepHandler self, string childName, GameObject footstepEffect)
        {
            orig(self, childName, footstepEffect);
            var charBody = self.gameObject.GetComponent<CharacterBody>();
            if (charBody && CheckForWater(transform.position)) Extinguish(charBody);
        }
        private void ExtinguishInWaterJump(On.RoR2.GlobalEventManager.orig_OnCharacterHitGround orig, GlobalEventManager self, CharacterBody characterBody, Vector3 impactVelocity)
        {
            orig(self, characterBody, impactVelocity);
            if (characterBody)
            {
                CharacterMotor characterMotor = characterBody.characterMotor;
                if (characterMotor && Run.FixedTimeStamp.now - characterMotor.lastGroundedTime > 0.2f)
                {
                    if (CheckForWater(characterBody.footPosition)) Extinguish(characterBody);
                }
            }
        }

        public static Vector3 GetHeadPosition(CharacterBody characterBody)
        {
            var dist = Vector3.Distance(characterBody.corePosition, characterBody.footPosition);
            return characterBody.corePosition + Vector3.up * dist;
        }


        private bool CheckForWaterOld(Vector3 position, bool below = true)
        {
            if (Physics.Raycast(new Ray(position + Vector3.up * 1.5f, below ? Vector3.down : Vector3.up), out RaycastHit raycastHit, below ? 4f : 8f, LayerIndex.world.mask | LayerIndex.water.mask, QueryTriggerInteraction.Collide))
            {
                SurfaceDef objectSurfaceDef = SurfaceDefProvider.GetObjectSurfaceDef(raycastHit.collider, raycastHit.point);
                if (objectSurfaceDef)
                {
                    if (objectSurfaceDef == waterSD)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        [ConCommand(commandName = "burn_self", flags = ConVarFlags.ExecuteOnServer,
            helpText = "burn_self {stacks} {duration}")]
        public static void MyCommandName(ConCommandArgs args)
        {
            DotController.DotIndex index = (DotController.DotIndex)Array.FindIndex(DotController.dotDefs, (dotDef) => dotDef.associatedBuff == RoR2Content.Buffs.OnFire);
            for (int y = 0; y < args.GetArgInt(0); y++)
            {
                DotController.InflictDot(args.senderBody.gameObject, args.senderBody.gameObject, index, args.GetArgInt(1), 0.25f);
                //args.senderBody.AddTimedBuffAuthority(BuffIndex.OnFire, args.GetArgInt(1));
            }
        }

        public class Extinguisher : MonoBehaviour
        {
            public CharacterBody characterBody;

            public void FixedUpdate()
            {
                if (CheckForWater(characterBody.corePosition))
                {
                    Extinguish(characterBody);
                    Destroy(this);
                }
            }

        }
    }
}
