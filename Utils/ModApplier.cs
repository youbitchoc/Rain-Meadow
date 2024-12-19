﻿using Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RainMeadow
{
    internal class ModApplier : ModManager.ModApplyer
    {
        public DialogAsyncWait? dialogBox;
        public Dialog? checkUserConfirmation;

        private readonly Menu.Menu menu;

        public event Action<ModApplier>? OnFinish;

        public ModApplier(ProcessManager manager, List<bool> pendingEnabled, List<int> pendingLoadOrder) : base(manager, pendingEnabled, pendingLoadOrder)
        {
            On.ModManager.ModApplyer.ApplyModsThread += ModApplyer_ApplyModsThread;
            On.RainWorld.Update += RainWorld_Update;
            menu = (Menu.Menu)manager.currentMainLoop;
        }

        private void ModApplyer_ApplyModsThread(On.ModManager.ModApplyer.orig_ApplyModsThread orig, ModManager.ModApplyer self)
        {
            // logging in a thread seems to break debug build
            //for (var i = 0; i < ModManager.InstalledMods.Count; i++)
            //{
            //    var mod = ModManager.InstalledMods[i];
            //    RainMeadow.Debug($"{mod.id}: {(pendingEnabled[i] ? "enabl" : "disabl")}{(pendingEnabled[i] == mod.enabled ? "ed" : "ing")}");
            //}
            orig(self);
        }

        private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
        {
            orig(self);

            Update();
        }

        public new void Update()
        {
            base.Update();

            dialogBox?.SetText(menu.Translate("mod_menu_apply_mods") + Environment.NewLine + statusText);

            if (IsFinished())
            {
                On.RainWorld.Update -= RainWorld_Update;
                if (dialogBox != null)
                {
                    dialogBox.RemoveSprites();
                    manager.dialog = null;
                    manager.ShowNextDialog();
                    dialogBox = null;
                }

                manager.rainWorld.options.Save();
                OnFinish?.Invoke(this);
            }
        }

        public void ShowConfirmation(List<ModManager.Mod> modsToEnable, List<ModManager.Mod> modsToDisable, List<string> unknownMods)
        {
            var modMismatchString = menu.Translate("Mod Mismatch!") + Environment.NewLine;

            if (modsToEnable.Count > 0)
                modMismatchString += Environment.NewLine + menu.Translate("Mods that have to be enabled: ") + string.Join(", ", modsToEnable.ConvertAll(mod => mod.LocalizedName));
            if (modsToDisable.Count > 0)
                modMismatchString += Environment.NewLine + menu.Translate("Mods that have to be disabled: ") + string.Join(", ", modsToDisable.ConvertAll(mod => mod.LocalizedName));
            if (unknownMods.Count > 0)
                modMismatchString += Environment.NewLine + menu.Translate("Mods that have to be installed: ") + string.Join(", ", unknownMods);

            // modMismatchString += Environment.NewLine + Environment.NewLine + menu.Translate("You will be returned to the Lobby Select screen");

            Action confirmProceed = () =>
            {
                manager.dialog = null;
                checkUserConfirmation = null;
                Start(filesInBadState);
            };

            Action cancelProceed = () =>
            {
                manager.dialog = null;
                checkUserConfirmation = null;
                OnlineManager.LeaveLobby();
                manager.RequestMainProcessSwitch(RainMeadow.Ext_ProcessID.LobbySelectMenu);
            };

            if (unknownMods.Any())
            {
                checkUserConfirmation = new DialogNotify(modMismatchString, new Vector2(480f, 320f), manager, cancelProceed);
            }
            else
            {
                checkUserConfirmation = new DialogConfirm(modMismatchString, new Vector2(480f, 320f), manager, confirmProceed, cancelProceed);
            }

            manager.ShowDialog(checkUserConfirmation);
        }
    }
}
