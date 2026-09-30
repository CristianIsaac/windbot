using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game;

namespace WindBot.Game.AI.Decks
{
    /// <summary>
    /// Edison Format (April 2010) Blackwing CPU.
    /// First playable implementation: conservative resource management,
    /// Whirlwind search priorities, Blackwing summons, Icarus targeting,
    /// Gale/Kalut combat logic and Edison staple handling.
    /// </summary>
    [Deck("Edison Blackwing", "AI_EdisonBlackwing")]
    public class EdisonBlackwingExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int Sirocco = 75498415;
            public const int Shura = 58820853;
            public const int Bora = 49003716;
            public const int Kalut = 85215458;
            public const int Gale = 2009101;
            public const int Blizzard = 22835145;
            public const int DarkArmedDragon = 65192027;
            public const int Gorz = 44330098;

            public const int BlackWhirlwind = 91351370;
            public const int AllureOfDarkness = 1475311;
            public const int BrainControl = 87910978;
            public const int BookOfMoon = 14087893;
            public const int MysticalSpaceTyphoon = 5318639;
            public const int HeavyStorm = 19613556;

            public const int IcarusAttack = 53567095;
            public const int MirrorForce = 44095762;
            public const int TorrentialTribute = 53582587;
            public const int BottomlessTrapHole = 29401950;
            public const int SolemnJudgment = 41420027;

            public const int StardustDragon = 44508094;
            public const int BlackRoseDragon = 73580471;
            public const int ColossalFighter = 23693634;
            public const int GoyoGuardian = 28053106;
            public const int Brionac = 50321796;
            public const int AllyOfJusticeCatastor = 26593852;
            public const int MagicalAndroid = 43385557;
            public const int MistWurm = 27315304;
            public const int ArmorMaster = 69031175;
            public const int ArmedWing = 76913983;
        }

        private static readonly int[] Blackwings =
        {
            CardId.Sirocco, CardId.Shura, CardId.Bora,
            CardId.Kalut, CardId.Gale, CardId.Blizzard
        };

        public EdisonBlackwingExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            // High-impact Edison staples first.
            AddExecutor(ExecutorType.Activate, CardId.HeavyStorm, HeavyStormEffect);
            AddExecutor(ExecutorType.Activate, CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, CardId.BrainControl);
            AddExecutor(ExecutorType.Activate, CardId.BookOfMoon, DefaultBookOfMoon);
            AddExecutor(ExecutorType.Activate, CardId.AllureOfDarkness, AllureEffect);

            // Blackwing engine.
            AddExecutor(ExecutorType.Activate, CardId.BlackWhirlwind, BlackWhirlwindEffect);
            AddExecutor(ExecutorType.Summon, CardId.Sirocco, SiroccoSummon);
            AddExecutor(ExecutorType.Summon, CardId.Shura, ShuraSummon);
            AddExecutor(ExecutorType.Summon, CardId.Blizzard, BlizzardSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Bora, BoraSpecialSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Gale, GaleSpecialSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Shura);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Bora);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Gale);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Kalut);

            AddExecutor(ExecutorType.Activate, CardId.Gale, GaleEffect);
            AddExecutor(ExecutorType.Activate, CardId.Kalut, KalutEffect);
            AddExecutor(ExecutorType.Activate, CardId.Shura);
            AddExecutor(ExecutorType.Activate, CardId.Blizzard);

            // Extra Deck. The engine itself validates legal materials.
            AddExecutor(ExecutorType.SpSummon, CardId.MistWurm);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.ColossalFighter);
            AddExecutor(ExecutorType.SpSummon, CardId.BlackRoseDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.ArmorMaster);
            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian);
            AddExecutor(ExecutorType.SpSummon, CardId.Brionac);
            AddExecutor(ExecutorType.SpSummon, CardId.ArmedWing);
            AddExecutor(ExecutorType.SpSummon, CardId.AllyOfJusticeCatastor);
            AddExecutor(ExecutorType.SpSummon, CardId.MagicalAndroid);

            // Reactive traps.
            AddExecutor(ExecutorType.Activate, CardId.IcarusAttack, IcarusAttackEffect);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.SolemnJudgment, DefaultSolemnJudgment);

            AddExecutor(ExecutorType.SpellSet, EdisonSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        private bool HasFaceUpBlackwing()
        {
            return Bot.GetMonsters().Any(card => card != null && card.IsFaceup() && card.IsCode(Blackwings));
        }

        private bool BlackWhirlwindEffect()
        {
            // Do not play a redundant second Whirlwind from hand unless the first is gone.
            if (Card.Location == CardLocation.Hand && Bot.HasInSpellZone(CardId.BlackWhirlwind))
                return false;

            // Search effect: Gale is the highest utility target, followed by Bora/Kalut/Blizzard.
            if (Card.Location != CardLocation.Hand)
                AI.SelectCard(CardId.Gale, CardId.Bora, CardId.Kalut, CardId.Blizzard, CardId.Shura);
            return true;
        }

        private bool SiroccoSummon()
        {
            // Sirocco's no-tribute summon is strongest when we are behind on board.
            return Bot.GetMonsterCount() == 0 && Enemy.GetMonsterCount() > 0;
        }

        private bool ShuraSummon()
        {
            ClientCard target = Enemy.GetMonsters().OrderBy(c => c.GetDefensePower()).FirstOrDefault();
            if (target == null)
                return true;
            return target.GetDefensePower() < 1800 || Bot.HasInHand(CardId.Kalut);
        }

        private bool BlizzardSummon()
        {
            return Bot.Graveyard.Any(card =>
                card != null && card.IsCode(CardId.Shura, CardId.Bora, CardId.Kalut));
        }

        private bool BoraSpecialSummon()
        {
            return HasFaceUpBlackwing() && Bot.GetMonsterCount() < 4;
        }

        private bool GaleSpecialSummon()
        {
            return HasFaceUpBlackwing() && Bot.GetMonsterCount() < 4;
        }

        private bool GaleEffect()
        {
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card != null && card.IsFaceup())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        private bool KalutEffect()
        {
            // Preserve Kalut unless its 1400 ATK swing changes the current battle.
            ClientCard attacker = Duel.Attacker;
            ClientCard defender = Duel.AttackTarget;
            if (attacker == null || defender == null || attacker.Controller != 0)
                return false;
            if (!attacker.IsCode(Blackwings))
                return false;

            int enemyPower = defender.GetDefensePower();
            return attacker.Attack <= enemyPower && attacker.Attack + 1400 > enemyPower;
        }

        private bool IcarusAttackEffect()
        {
            if (!Bot.GetMonsters().Any(card => card != null && card.IsCode(Blackwings)))
                return false;

            List<ClientCard> targets = Enemy.GetMonsters()
                .Concat(Enemy.GetSpells())
                .Where(card => card != null)
                .OrderByDescending(card => card.IsFaceup() ? 1 : 0)
                .Take(2)
                .ToList();

            if (targets.Count < 2)
                return false;

            ClientCard tribute = Bot.GetMonsters()
                .Where(card => card != null && card.IsCode(Blackwings))
                .OrderBy(card => card.Attack)
                .FirstOrDefault();

            AI.SelectCard(tribute);
            AI.SelectNextCard(targets);
            return true;
        }

        private bool HeavyStormEffect()
        {
            int enemyBackrow = Enemy.GetSpellCountWithoutField();
            int ownBackrow = Bot.GetSpellCountWithoutField();
            return enemyBackrow >= 2 && enemyBackrow > ownBackrow;
        }

        private bool AllureEffect()
        {
            // Avoid Allure when there is no DARK monster available to pay its resolution.
            return Bot.Hand.Any(card => card != null && card.HasAttribute(CardAttribute.Dark) &&
                !card.IsCode(CardId.AllureOfDarkness));
        }

        private bool EdisonSpellSet()
        {
            // Keep quick-play interaction available; don't set power spells unnecessarily.
            if (Card.IsCode(CardId.BookOfMoon, CardId.MysticalSpaceTyphoon))
                return Bot.GetSpellCountWithoutField() < 4;
            return DefaultSpellSet();
        }
    }
}
