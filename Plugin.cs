using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;

[assembly: AssemblyVersion(Local.HealthBar.Viewer.Plugin.versionNumber)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
		// Allow private member access via publicized assemblies.

namespace Local.HealthBar.Viewer
{
	[BepInPlugin("local.healthbar.viewer", "HealthBarViewer", versionNumber)]
	public class Plugin : BaseUnityPlugin
	{
		public const string versionNumber = "0.1.1";
		private static float duration, threshold, range, delay, interval;

		public void Awake()
		{
			const string general = "General", other = "Other";
			const float percent = 100, millisecond = 1000;

			duration = Config.Bind(
					section: general,
					key: "Minimum Duration",
					defaultValue: 8u,
					description:
						"After an ally deals damage, the target's health bar will remain " +
						"visible for this many seconds."
				).Value;

			threshold = Config.Bind(
					section: general,
					key: "Health Threshold",
					defaultValue: 75f,
					new ConfigDescription(
						"Enemies at this hit point percentage (or below) remain visible " +
						"indefinitely.",
						new AcceptableValueRange<float>(0, percent))
				).Value / percent;

			range = Config.Bind(
					section: general,
					key: "Maximum Range",
					defaultValue: 60u,
					description:
						"Remove health bar regardless of health threshold if distance to " +
						"target exceeds this value in meters. Set to zero for unlimited range."
				).Value;

			delay = Config.Bind(
					section: other,
					key: "Targeting Delay",
					defaultValue: 0u,
					description:
						"Aiming directly at an enemy will refresh the health bar indicator. " +
						"This extends the duration for the specified number of milliseconds. " +
						"However, minimum duration parameter is used instead for targets " +
						"below the health threshold."
				).Value / millisecond;

			interval = Config.Bind(
					section: other,
					key: "Refresh Interval",
					defaultValue: 500u,
					description:
						"How often to check target health/range (in milliseconds). Note that " +
						"decreasing this value could negatively affect performance."
				).Value / millisecond;

			ReplaceEventHandler();
			Harmony.CreateAndPatchAll(typeof(Plugin));
		}

		private static void ReplaceEventHandler()
		{
			try
			{
				Type healthBarViewer = typeof(CombatHealthBarViewer),
						eventManager = typeof(GlobalEventManager);
				string eventName = nameof(GlobalEventManager.onClientDamageNotified);

				RuntimeHelpers.RunClassConstructor(healthBarViewer.TypeHandle);

				EventInfo eventInfo = eventManager.GetEvent(eventName);
				Delegate eventHandler = eventManager.GetField(
						eventName, BindingFlags.Static | BindingFlags.NonPublic
					).GetValue(null) as Delegate;

				foreach ( Delegate subscriber in eventHandler.GetInvocationList() )
					if ( healthBarViewer == subscriber.Method.DeclaringType.DeclaringType )
					{
						eventInfo.RemoveEventHandler(null, subscriber);
						return;
					}

				throw new Exception("Unable to locate original health bar event handler.");
			}
			catch ( Exception exception )
			{
				System.Console.WriteLine(exception);
			}
			finally
			{
				GlobalEventManager.onClientDamageNotified += ShowHealthBar;
			}
		}

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Awake))]
		[HarmonyPrefix]
		private static void IncreaseDuration(CombatHealthBarViewer __instance)
				=> __instance.healthBarDuration = duration;

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.CleanUp))]
		[HarmonyPrefix]
		private static bool CheckStatus(CombatHealthBarViewer __instance)
		{
			for ( int i = __instance.trackedVictims.Count; --i >= 0; )
			{
				HealthComponent target = __instance.trackedVictims[i];
				CombatHealthBarViewer.HealthBarInfo
						healthBarInfo = __instance.GetHealthBarInfo(target);

				if ( target && target.alive && target.body )
				{
					if ( healthBarInfo.endTime > Time.time ) continue;

					float distance = range > 0 && __instance.viewerBody ? (
							__instance.viewerBody.corePosition - target.body.corePosition
						).magnitude : range;

					if ( target.combinedHealthFraction <= threshold && distance <= range )
					{
						healthBarInfo.endTime = Time.time + interval;
						continue;
					}
				}

				__instance.Remove(i, healthBarInfo);
			}

			return false;
		}

		private static void ShowHealthBar(DamageDealtMessage message)
		{
			if ( message.isSilent || ! message.victim || ! message.attacker ) return;

			HealthComponent target = message.victim.GetComponent<HealthComponent>();
			if ( ! target || target.dontShowHealthbar ) return;

			TeamIndex attacker = TeamComponent.GetObjectTeam(message.attacker),
					victim = TeamComponent.GetObjectTeam(message.victim);

			foreach ( CombatHealthBarViewer viewer in CombatHealthBarViewer.instancesList )
				if ( viewer.viewerBodyObject && attacker == viewer.viewerTeamIndex )
					viewer.HandleDamage(target, victim);
		}

		[HarmonyPatch(typeof(CameraRigController), nameof(CameraRigController.Update))]
		[HarmonyPostfix]
		private static void UpdateCrosshair(CameraRigController __instance)
		{
			var data = __instance.cameraMode?.camToRawInstanceData.GetValueSafe(__instance)
					as RoR2.CameraModes.CameraModePlayerBasic.InstanceData;

			__instance.lastCrosshairHurtBox = data?.lastCrosshairHurtBox;
		}

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Update))]
		[HarmonyPrefix]
		private static bool ShowCrosshairTarget(CombatHealthBarViewer __instance)
		{
			if ( __instance.crosshairTarget )
			{
				float delay = Plugin.delay;
				if ( __instance.crosshairTarget.combinedHealthFraction <= threshold )
					delay = duration;

				if ( delay > 0 )
				{
					CombatHealthBarViewer.HealthBarInfo
							info = __instance.GetHealthBarInfo(__instance.crosshairTarget);
					info.endTime = Mathf.Max(info.endTime, Time.time + delay);
				}
			}

			__instance.SetDirty();
			return false;
		}
	}
}
