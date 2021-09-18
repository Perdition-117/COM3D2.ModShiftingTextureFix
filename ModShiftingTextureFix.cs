using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.ModShiftingTextureFix {
	class ResourceReference {
		public string BaseItem;
		public string BaseModItem;
	}

	[BepInPlugin("net.perdition.com3d2.modshiftingtexturefix", "ModShiftingTextureFix", "1.0.0.0")]
	public class ModShiftingTextureFix : BaseUnityPlugin {
		static readonly Dictionary<string, ResourceReference> ModResourceReferences = new();

		void Awake() {
			Harmony.CreateAndPatchAll(typeof(ModShiftingTextureFix));
		}

		[HarmonyPatch(typeof(Menu), "ProcScriptBin", typeof(Maid), typeof(byte[]), typeof(MaidProp), typeof(bool), typeof(SubProp))]
		[HarmonyPostfix]
		static void PostProcScriptBin(Maid maid, byte[] cd, MaidProp mp, bool f_bTemp = false, SubProp f_SubProp = null) {
			var fileName = f_bTemp ? mp.strTempFileName : mp.strFileName;
			if (fileName.IndexOf("mod_") == 0) {
				var hashCode = Path.GetFileName(fileName).ToLower().GetHashCode();
				if (Menu.m_dicResourceRef.TryGetValue(hashCode, out var resourceReferences)) {
					foreach (var resourceReference in resourceReferences) {
						// .mod items normally have their shifting reference item set to the base item
						// we set the reference item to a "virtual" item instead, to be used in ProcScript
						var tempItemName = Path.GetFileNameWithoutExtension(fileName) + resourceReference.Key + ".mod";
						resourceReferences[resourceReference.Key] = tempItemName;
						ModResourceReferences[tempItemName] = new() {
							BaseItem = resourceReference.Value,
							BaseModItem = fileName,
						};
					}
				}
			}
		}

		[HarmonyPatch(typeof(Menu), "ProcScript", typeof(Maid), typeof(MaidProp), typeof(bool), typeof(SubProp))]
		[HarmonyPrefix]
		static void PreProcScript(ref string __state, Maid maid, MaidProp mp, bool f_bTemp = false, SubProp f_SubProp = null) {
			// temporarily set the MaidProp file name back to what it was in order for ProcScript to load the appropriate base item
			var fileName = f_bTemp ? mp.strTempFileName : mp.strFileName;
			if (ModResourceReferences.TryGetValue(fileName, out var baseItem)) {
				__state = fileName;
				if (f_bTemp) {
					mp.strTempFileName = baseItem.BaseItem;
				} else {
					mp.strFileName = baseItem.BaseItem;
				}
			}
		}

		[HarmonyPatch(typeof(Menu), "ProcScript", typeof(Maid), typeof(MaidProp), typeof(bool), typeof(SubProp))]
		[HarmonyPostfix]
		static void PostProcScript(ref string __state, Maid maid, MaidProp mp, bool f_bTemp = false, SubProp f_SubProp = null) {
			// load the mod texture
			var fileName = __state;
			if (fileName != null && ModResourceReferences.TryGetValue(fileName, out var baseItem)) {
				if (f_bTemp) {
					mp.strTempFileName = fileName;
				} else {
					mp.strFileName = fileName;
				}
				var text2 = Menu.GetModPathFileName(baseItem.BaseModItem);
				if (string.IsNullOrEmpty(text2)) {
					return;
				}
				try {
					var array = File.ReadAllBytes(text2);
					if (array == null) {
						Debug.LogWarning("MOD item menu file not found." + fileName);
						return;
					}
					Menu.ProcModScriptBin(maid, array, text2, mp, false);
				} catch (Exception ex) {
					Debug.LogError("ProcScript Could not load the MOD item menu file. : " + fileName + " : " + ex.Message);
					return;
				}
			}
		}
	}
}
