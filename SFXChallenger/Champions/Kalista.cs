﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Kalista.cs is part of SFXChallenger.

 SFXChallenger is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXChallenger is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXChallenger. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXChallenger.Abstracts;
using SFXChallenger.Enumerations;
using SFXChallenger.Helpers;
using SFXChallenger.Managers;
using SFXChallenger.Wrappers;
using SFXLibrary;
using SFXLibrary.Logger;
using SharpDX;
using Collision = LeagueSharp.Common.Collision;
using MinionManager = SFXLibrary.MinionManager;
using MinionOrderTypes = SFXLibrary.MinionOrderTypes;
using MinionTeam = SFXLibrary.MinionTeam;
using MinionTypes = SFXLibrary.MinionTypes;
using Orbwalking = SFXChallenger.Wrappers.Orbwalking;
using TargetSelector = SFXChallenger.Wrappers.TargetSelector;
using Utils = SFXChallenger.Helpers.Utils;

#endregion

namespace SFXChallenger.Champions
{
    internal class Kalista : Champion
    {
        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override void OnLoad()
        {
            Obj_AI_Base.OnBuffAdd += OnObjAiBaseBuffAdd;
            Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
            Spellbook.OnCastSpell += OnSpellbookCastSpell;
            Orbwalking.OnNonKillableMinion += OnOrbwalkingNonKillableMinion;
            Orbwalking.AfterAttack += OnOrbwalkingAfterAttack;
            Core.OnPreUpdate += OnCorePreUpdate;

            var buffHero =
                GameObjects.AllyHeroes.FirstOrDefault(
                    a =>
                        a.Buffs.Any(
                            b =>
                                b.Caster.IsMe &&
                                b.Name.Equals("kalistacoopstrikeally", StringComparison.OrdinalIgnoreCase)));
            if (buffHero != null)
            {
                SoulBound.Unit = buffHero;
            }
        }

        protected override void OnUnload()
        {
            Obj_AI_Base.OnBuffAdd -= OnObjAiBaseBuffAdd;
            Obj_AI_Base.OnProcessSpellCast -= OnObjAiBaseProcessSpellCast;
            Spellbook.OnCastSpell -= OnSpellbookCastSpell;
            Orbwalking.OnNonKillableMinion -= OnOrbwalkingNonKillableMinion;
            Orbwalking.AfterAttack -= OnOrbwalkingAfterAttack;
            Core.OnPreUpdate -= OnCorePreUpdate;
        }

        protected override void AddToMenu()
        {
            var comboMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Combo"), Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, int> { { "Q", 2 } });
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));
            comboMenu.AddItem(
                new MenuItem(comboMenu.Name + ".e-min", "E " + Global.Lang.Get("G_Min")).SetValue(new Slider(10, 1, 20)));

            var harassMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Harass"), Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, int> { { "Q", 2 } });
            ManaManager.AddToMenu(harassMenu, "harass", ManaCheckType.Minimum, ManaValueType.Percent);
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));
            harassMenu.AddItem(
                new MenuItem(harassMenu.Name + ".e-min", "E " + Global.Lang.Get("G_Min")).SetValue(new Slider(5, 1, 15)));

            var laneclearMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LaneClear"), Menu.Name + ".lane-clear"));
            ManaManager.AddToMenu(laneclearMenu, "lane-clear", ManaCheckType.Minimum, ManaValueType.Percent);
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".q-min-1", "Q " + Global.Lang.Get("G_Min") + " <= 4").SetValue(
                    new Slider(2, 1, 5)));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".q-min-2", "Q " + Global.Lang.Get("G_Min") + " <= 7").SetValue(
                    new Slider(3, 1, 5)));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".q-min-3", "Q " + Global.Lang.Get("G_Min") + " >= 10").SetValue(
                    new Slider(5, 1, 5)));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(false));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".e-min", "E " + Global.Lang.Get("G_Min")).SetValue(
                    new Slider(2, 1, 5)));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".e-jungle", "E " + Global.Lang.Get("G_Jungle")).SetValue(true));

            var lasthitMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LastHit"), Menu.Name + ".lasthit"));
            ManaManager.AddToMenu(lasthitMenu, "lasthit", ManaCheckType.Minimum, ManaValueType.Percent);
            lasthitMenu.AddItem(
                new MenuItem(lasthitMenu.Name + ".e-big", "E " + Global.Lang.Get("G_Big")).SetValue(true));
            lasthitMenu.AddItem(
                new MenuItem(lasthitMenu.Name + ".e-unkillable", "E " + Global.Lang.Get("G_Unkillable")).SetValue(true));
            lasthitMenu.AddItem(
                new MenuItem(lasthitMenu.Name + ".e-turret", "E " + Global.Lang.Get("G_Turret")).SetValue(true));

            var killstealMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Killsteal"), Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var fleeMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Flee"), Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".aa", Global.Lang.Get("G_UseAutoAttacks")).SetValue(true));

            var ultiMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Ultimate"), Menu.Name + ".ultimate"));

            var blitzMenu = ultiMenu.AddSubMenu(new Menu("Blitzcrank", ultiMenu.Name + ".blitzcrank"));

            var excludeMenu =
                blitzMenu.AddSubMenu(new Menu(Global.Lang.Get("G_Blacklist"), blitzMenu.Name + ".blacklist"));
            foreach (var enemy in GameObjects.EnemyHeroes)
            {
                excludeMenu.AddItem(
                    new MenuItem(excludeMenu.Name + enemy.ChampionName, enemy.ChampionName).SetValue(false));
            }

            blitzMenu.AddItem(new MenuItem(blitzMenu.Name + ".r", Global.Lang.Get("G_UseR")).SetValue(true));

            ultiMenu.AddItem(new MenuItem(ultiMenu.Name + ".save", Global.Lang.Get("G_Save")).SetValue(true));


            var miscMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Miscellaneous"), Menu.Name + ".miscellaneous"));
            ManaManager.AddToMenu(miscMenu, "misc", ManaCheckType.Minimum, ManaValueType.Percent);
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".e-reset", Global.Lang.Get("Kalista_EHarassReset")).SetValue(true));
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".w-baron", Global.Lang.Get("Kalista_WBaron")).SetValue(
                    new KeyBind('J', KeyBindType.Press)));
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".w-dragon", Global.Lang.Get("Kalista_WDragon")).SetValue(
                    new KeyBind('K', KeyBindType.Press)));

            DrawingManager.Add("E Damage", new Circle(true, DamageIndicator.DrawingColor)).ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs args)
                {
                    DamageIndicator.Enabled = args.GetNewValue<Circle>().Active;
                    DamageIndicator.DrawingColor = args.GetNewValue<Circle>().Color;
                };

            DamageIndicator.Initialize(Rend.GetDamage);
            DamageIndicator.Enabled = DrawingManager.Get("E Damage").GetValue<Circle>().Active;
            DamageIndicator.DrawingColor = DrawingManager.Get("E Damage").GetValue<Circle>().Color;
            TargetSelector.OverwriteWeightFunction("low-health", hero => hero.Health - Rend.GetDamage(hero));
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 1200f);
            Q.SetSkillshot(0.25f, 40f, 1650f, true, SkillshotType.SkillshotLine);

            W = new Spell(SpellSlot.W, 5000f);

            E = new Spell(SpellSlot.E, 1000f);

            R = new Spell(SpellSlot.R, 1200f);
        }

        private void OnObjAiBaseBuffAdd(Obj_AI_Base sender, Obj_AI_BaseBuffAddEventArgs args)
        {
            try
            {
                if (args.Buff.Caster.IsMe && sender.IsAlly &&
                    args.Buff.Name.Equals("kalistapaltarbuff", StringComparison.OrdinalIgnoreCase))
                {
                    var hero = sender as Obj_AI_Hero;
                    if (hero != null)
                    {
                        SoulBound.Unit = hero;
                    }
                }
                var target = sender as Obj_AI_Hero;
                if (target != null)
                {
                    if (SoulBound.Unit != null && sender.IsEnemy &&
                        args.Buff.Caster.NetworkId == SoulBound.Unit.NetworkId &&
                        args.Buff.Name.Equals("rocketgrab2", StringComparison.OrdinalIgnoreCase) && args.Buff.IsActive)
                    {
                        if (Menu.Item(Menu.Name + ".ultimate.blitzcrank.r").GetValue<bool>() &&
                            !Menu.Item(Menu.Name + ".ultimate.blitzcrank.blacklist." + target.ChampionName)
                                .GetValue<bool>() && R.IsReady() && SoulBound.Unit.Distance(Player) < R.Range)
                        {
                            if (!SoulBound.Unit.UnderTurret(false) && SoulBound.Unit.Distance(sender) > 750f &&
                                SoulBound.Unit.Distance(Player) > R.Range / 3f)
                            {
                                R.Cast();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnSpellbookCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            try
            {
                if (sender.Owner.IsMe && args.Slot == SpellSlot.Q && Player.IsDashing())
                {
                    args.Process = false;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            try
            {
                if (sender.IsMe)
                {
                    if (args.SData.Name == "KalistaExpungeWrapper")
                    {
                        Orbwalking.ResetAutoAttackTimer();
                    }
                }
                if (!sender.IsEnemy || SoulBound.Unit == null || R.Level == 0 ||
                    !Menu.Item(Menu.Name + ".ultimate.save").GetValue<bool>())
                {
                    return;
                }
                if (args.Target != null && args.Target.NetworkId == SoulBound.Unit.NetworkId &&
                    (!(sender is Obj_AI_Hero) || args.SData.IsAutoAttack()))
                {
                    SoulBound.Add(
                        SoulBound.Unit.ServerPosition.Distance(sender.ServerPosition) / args.SData.MissileSpeed +
                        Game.Time, (float) sender.GetAutoAttackDamage(SoulBound.Unit));
                }
                else
                {
                    var hero = sender as Obj_AI_Hero;
                    if (hero != null)
                    {
                        var slot = hero.GetSpellSlot(args.SData.Name);
                        if (slot != SpellSlot.Unknown)
                        {
                            var damage = 0f;
                            if (args.Target != null && args.Target.NetworkId == SoulBound.Unit.NetworkId &&
                                slot == hero.GetSpellSlot("SummonerDot"))
                            {
                                damage =
                                    (float) hero.GetSummonerSpellDamage(SoulBound.Unit, Damage.SummonerSpell.Ignite);
                            }
                            else if ((slot == SpellSlot.Q || slot == SpellSlot.W || slot == SpellSlot.E ||
                                      slot == SpellSlot.R) &&
                                     ((args.Target != null && args.Target.NetworkId == SoulBound.Unit.NetworkId) ||
                                      args.End.Distance(SoulBound.Unit.ServerPosition) <
                                      Math.Pow(args.SData.LineWidth, 2)))
                            {
                                damage = (float) hero.GetSpellDamage(SoulBound.Unit, slot);
                            }
                            if (damage > 0)
                            {
                                SoulBound.Add(Game.Time + 2, damage);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            Orbwalker.ForceTarget(null);
            if (unit.IsMe && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                var enemy = target as Obj_AI_Hero;
                if (enemy != null)
                {
                    ItemManager.UseComboItems(enemy);
                    SummonerManager.UseComboSummoners(enemy);
                }
            }
        }

        private void OnOrbwalkingNonKillableMinion(AttackableUnit unit)
        {
            try
            {
                if (Menu.Item(Menu.Name + ".lasthit.e-unkillable").GetValue<bool>() && E.IsReady() &&
                    ManaManager.Check("lasthit"))
                {
                    var target = unit as Obj_AI_Base;
                    if (target != null && Rend.IsKillable(target, true))
                    {
                        E.Cast();
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnCorePreUpdate(EventArgs args)
        {
            try
            {
                if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo &&
                    Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Flee)
                {
                    var eBig = Menu.Item(Menu.Name + ".lasthit.e-big").GetValue<bool>();
                    var eTurret = Menu.Item(Menu.Name + ".lasthit.e-turret").GetValue<bool>();
                    var eReset = Menu.Item(Menu.Name + ".miscellaneous.e-reset").GetValue<bool>();

                    IEnumerable<Obj_AI_Minion> minions = new HashSet<Obj_AI_Minion>();
                    if (eBig || eTurret || eReset)
                    {
                        minions =
                            GameObjects.EnemyMinions.Where(e => e.IsValidTarget(E.Range) && Rend.IsKillable(e, true));
                    }

                    if (E.IsReady())
                    {
                        if (eBig)
                        {
                            var creeps =
                                GameObjects.Jungle.Where(e => e.IsValidTarget(E.Range) && Rend.IsKillable(e, false))
                                    .Concat(minions)
                                    .ToList();
                            if (
                                creeps.Any(
                                    m =>
                                        (m.CharData.BaseSkinName.Contains("MinionSiege") ||
                                         m.CharData.BaseSkinName.Contains("Super") ||
                                         m.CharData.BaseSkinName.StartsWith("SRU_Dragon") ||
                                         m.CharData.BaseSkinName.StartsWith("SRU_Baron"))))
                            {
                                E.Cast();
                                return;
                            }
                        }

                        if (eTurret && ManaManager.Check("lasthit"))
                        {
                            var minion = minions.FirstOrDefault(m => Utils.UnderAllyTurret(m.Position));
                            if (minion != null)
                            {
                                E.Cast();
                                return;
                            }
                        }
                    }

                    if (eReset && E.IsReady() && ManaManager.Check("misc") &&
                        GameObjects.EnemyHeroes.Any(e => Rend.HasBuff(e) && e.IsValidTarget(E.Range)))
                    {
                        if (minions.Any())
                        {
                            E.Cast();
                            return;
                        }
                    }
                }
                if (Menu.Item(Menu.Name + ".ultimate.save").GetValue<bool>() && SoulBound.Unit != null && R.IsReady())
                {
                    SoulBound.Clean();
                    if (SoulBound.Unit.HealthPercent <= 10 && SoulBound.Unit.CountEnemiesInRange(500) > 0 ||
                        (SoulBound.Unit.HealthPercent <= 45 && SoulBound.TotalDamage * 1.1f > SoulBound.Unit.Health))
                    {
                        R.Cast();
                    }
                }
                if (Menu.Item(Menu.Name + ".miscellaneous.w-baron").GetValue<KeyBind>().Active && W.IsReady() &&
                    Player.Distance(SummonersRift.River.Baron) <= W.Range)
                {
                    W.Cast(SummonersRift.River.Baron);
                }
                if (Menu.Item(Menu.Name + ".miscellaneous.w-dragon").GetValue<KeyBind>().Active && W.IsReady() &&
                    Player.Distance(SummonersRift.River.Dragon) <= W.Range)
                {
                    W.Cast(SummonersRift.River.Dragon);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void Combo()
        {
            var useQ = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady() &&
                       ((Q.Instance.ManaCost + E.Instance.ManaCost) < Player.Mana);
            var useE = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady();

            if (useQ)
            {
                Casting.SkillShot(Q, Q.GetHitChance("combo"));
            }

            if (useE)
            {
                var target = TargetSelector.GetTarget(
                    E.Range * 1.2f, LeagueSharp.Common.TargetSelector.DamageType.Physical);
                if (target != null && Rend.HasBuff(target))
                {
                    if (target.Distance(Player) > Orbwalking.GetRealAutoAttackRange(target))
                    {
                        var minion =
                            GameObjects.EnemyMinions.FirstOrDefault(
                                m => m.IsValidTarget(Orbwalking.GetRealAutoAttackRange(m)) && Rend.IsKillable(m, true));
                        if (minion != null)
                        {
                            E.Cast();
                        }
                    }
                    else if (E.IsInRange(target))
                    {
                        if (Rend.IsKillable(target, false))
                        {
                            E.Cast();
                        }
                        else
                        {
                            var buff = Rend.GetBuff(target);
                            if (buff != null &&
                                buff.Count >= Menu.Item(Menu.Name + ".combo.e-min").GetValue<Slider>().Value)
                            {
                                if (target.Distance(Player) > E.Range * 0.8 || buff.EndTime - Game.Time < 0.3)
                                {
                                    E.Cast();
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void Harass()
        {
            if (!ManaManager.Check("harass"))
            {
                return;
            }
            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>() && Q.IsReady())
            {
                Casting.SkillShot(Q, Q.GetHitChance("harass"));
            }
            if (Menu.Item(Menu.Name + ".harass.e").GetValue<bool>() && E.IsReady())
            {
                foreach (var enemy in GameObjects.EnemyHeroes.Where(e => E.IsInRange(e)))
                {
                    if (Rend.IsKillable(enemy, true))
                    {
                        E.Cast();
                    }
                    else
                    {
                        var buff = Rend.GetBuff(enemy);
                        if (buff != null &&
                            buff.Count >= Menu.Item(Menu.Name + ".harass.e-min").GetValue<Slider>().Value)
                        {
                            if (enemy.Distance(Player) > E.Range * 0.8 || buff.EndTime - Game.Time < 0.3)
                            {
                                E.Cast();
                            }
                        }
                    }
                }
            }
        }

        private List<Obj_AI_Base> QGetCollisions(Obj_AI_Hero source, Vector3 targetposition)
        {
            var input = new PredictionInput
            {
                Unit = source,
                Radius = Q.Width,
                Delay = Q.Delay,
                Speed = Q.Speed,
            };
            input.CollisionObjects[0] = CollisionableObjects.Minions;
            return Collision.GetCollision(new List<Vector3> { targetposition }, input).OrderBy(obj => obj.Distance(source)).ToList();
        }

        protected override void LaneClear()
        {
            if (!ManaManager.Check("lane-clear"))
            {
                return;
            }

            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var useE = Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady();

            if (!useQ && !useE)
            {
                return;
            }

            var minQ1 = Menu.Item(Menu.Name + ".lane-clear.q-min-1").GetValue<Slider>().Value;
            var minQ2 = Menu.Item(Menu.Name + ".lane-clear.q-min-2").GetValue<Slider>().Value;
            var minQ3 = Menu.Item(Menu.Name + ".lane-clear.q-min-3").GetValue<Slider>().Value;
            var minE = Menu.Item(Menu.Name + ".lane-clear.e-min").GetValue<Slider>().Value;
            var minQ = 0;
            var minions = MinionManager.GetMinions(
                Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minions.Count == 0)
            {
                return;
            }
            if (minions.Count >= 10)
            {
                minQ = minQ3;
            }
            else if (minions.Count <= 7)
            {
                minQ = minQ2;
            }
            else if (minions.Count <= 4)
            {
                minQ = minQ1;
            }
            if (useQ && minions.Count >= minQ && !Player.IsWindingUp && !Player.IsDashing())
            {
                foreach (var minion in minions.Where(x => x.Health <= Q.GetDamage(x)))
                {
                    var killcount = 0;

                    foreach (var colminion in QGetCollisions(Player, Player.ServerPosition.Extend(minion.ServerPosition, Q.Range)))
                    {
                        if (colminion.Health <= Q.GetDamage(colminion))
                        {
                            killcount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (killcount >= minQ)
                    {
                        Q.Cast(minion.ServerPosition);
                        break;
                    }
                }
            }
            if (useE)
            {
                var killable = minions.Where(m => E.IsInRange(m) && Rend.IsKillable(m, false)).ToList();
                if (killable.Count >= minE ||
                    (killable.Count >= 1 && Menu.Item(Menu.Name + ".lane-clear.e-jungle").GetValue<bool>() &&
                     killable.Any(m => m.Team == GameObjectTeam.Neutral)))
                {
                    E.Cast();
                }
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.aa").GetValue<bool>())
            {
                var dashObjects = GetDashObjects();
                if (dashObjects != null && dashObjects.Any())
                {
                    Orbwalking.Orbwalk(dashObjects.First(), Game.CursorPos);
                }
            }
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".killsteal.e").GetValue<bool>() && E.IsReady() &&
                GameObjects.EnemyHeroes.Any(h => h.IsValidTarget(E.Range) && Rend.IsKillable(h, false)))
            {
                E.Cast();
            }
        }

        public static IOrderedEnumerable<Obj_AI_Base> GetDashObjects()
        {
            try
            {
                var objects =
                    GameObjects.Enemy.Where(o => o.IsValidTarget(Orbwalking.GetRealAutoAttackRange(o))).ToList();
                var apexPoint = ObjectManager.Player.ServerPosition.To2D() +
                                (ObjectManager.Player.ServerPosition.To2D() - Game.CursorPos.To2D()).Normalized() *
                                Orbwalking.GetRealAutoAttackRange(ObjectManager.Player);
                return
                    objects.Where(
                        o =>
                            Utils.IsLyingInCone(
                                o.ServerPosition.To2D(), apexPoint, ObjectManager.Player.ServerPosition.To2D(), Math.PI))
                        .OrderBy(o => o.Distance(apexPoint, true));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return null;
        }

        internal class SoulBound
        {
            public static Dictionary<float, float> Damages = new Dictionary<float, float>();
            public static Obj_AI_Hero Unit { get; set; }

            public static float TotalDamage
            {
                get
                {
                    try
                    {
                        if (Damages.Count > 0)
                        {
                            lock (Damages)
                            {
                                return Damages.Sum(e => e.Value);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.AddItem(new LogItem(ex));
                    }
                    return 0f;
                }
            }

            public static void Clean()
            {
                try
                {
                    lock (Damages)
                    {
                        foreach (var entry in Damages.Where(entry => Game.Time > entry.Key))
                        {
                            Damages.Remove(entry.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }

            public static void Add(float time, float damage)
            {
                try
                {
                    lock (Damages)
                    {
                        float value;
                        Damages.TryGetValue(time, out value);
                        Damages[time] = value + damage;
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }
        }

        internal class Rend
        {
            private static readonly float[] Damage = { 20, 30, 40, 50, 60 };
            private static readonly float[] DamageMultiplier = { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };
            private static readonly float[] DamagePerSpear = { 10, 14, 19, 25, 32 };
            private static readonly float[] DamagePerSpearMultiplier = { 0.2f, 0.225f, 0.25f, 0.275f, 0.3f };

            public static bool IsKillable(Obj_AI_Base target, bool check)
            {
                if (check)
                {
                    if (target.Health < 150 && target is Obj_AI_Minion)
                    {
                        if (HealthPrediction.GetHealthPrediction(target, 150) <= 0)
                        {
                            return false;
                        }
                    }
                }
                return GetDamage(target) > target.Health + target.AttackShield;
            }

            private static float GetRealDamage(Obj_AI_Base target, float damage)
            {
                if (target is Obj_AI_Minion)
                {
                    var dragonBuff =
                        ObjectManager.Player.Buffs.FirstOrDefault(
                            b => b.Name.Equals("s5test_dragonslayerbuff", StringComparison.OrdinalIgnoreCase));
                    if (dragonBuff != null)
                    {
                        if (dragonBuff.Count == 4)
                        {
                            damage *= 1.15f;
                        }
                        else if (dragonBuff.Count == 5)
                        {
                            damage *= 1.3f;
                        }
                        if (target.CharData.BaseSkinName.StartsWith("SRU_Dragon"))
                        {
                            damage *= 1f - 0.07f * dragonBuff.Count;
                        }
                    }
                    if (target.CharData.BaseSkinName.StartsWith("SRU_Baron"))
                    {
                        var baronBuff =
                            ObjectManager.Player.Buffs.FirstOrDefault(
                                b => b.Name.Equals("barontarget", StringComparison.OrdinalIgnoreCase));
                        if (baronBuff != null)
                        {
                            damage *= 0.5f;
                        }
                    }
                }
                damage -= target.HPRegenRate / 2f;
                if (ObjectManager.Player.HasBuff("summonerexhaust"))
                {
                    damage *= 0.6f;
                }
                return damage;
            }

            public static float GetDamage(Obj_AI_Hero target)
            {
                return GetDamage(target, -1);
            }

            public static float GetDamage(Obj_AI_Base target, int customStacks = -1)
            {
                return GetRealDamage(
                    target,
                    100 /
                    (100 + (target.Armor * ObjectManager.Player.PercentArmorPenetrationMod) -
                     ObjectManager.Player.FlatArmorPenetrationMod) * GetRawDamage(target, customStacks) - 10);
            }

            public static float GetRawDamage(Obj_AI_Base target, int customStacks = -1)
            {
                try
                {
                    var buff = GetBuff(target);
                    var eLevel = ObjectManager.Player.GetSpell(SpellSlot.E).Level;
                    if (((buff != null && buff.Count > 0) || customStacks > -1) && eLevel > 0)
                    {
                        var damage = (Damage[eLevel - 1] +
                                      DamageMultiplier[eLevel - 1] * ObjectManager.Player.TotalAttackDamage()) +
                                     ((customStacks < 0 && buff != null ? buff.Count : customStacks) - 1) *
                                     (DamagePerSpear[eLevel - 1] +
                                      DamagePerSpearMultiplier[eLevel - 1] *
                                      (ObjectManager.Player.BaseAttackDamage +
                                       ObjectManager.Player.FlatPhysicalDamageMod));
                        return damage;
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
                return 0f;
            }

            public static bool HasBuff(Obj_AI_Base target)
            {
                return GetBuff(target) != null;
            }

            public static BuffInstance GetBuff(Obj_AI_Base target)
            {
                return
                    target.Buffs.FirstOrDefault(
                        b => b.Caster.IsMe && b.IsValidBuff() && b.DisplayName == "KalistaExpungeMarker");
            }
        }
    }
}