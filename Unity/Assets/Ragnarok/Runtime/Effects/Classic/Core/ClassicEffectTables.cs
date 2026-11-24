// Synced from reference/port/src/RoClassicFx/ClassicEffectTables.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public static partial class ClassicEffectTables
    {
        /// <summary>
        /// Complete STR selection of CRagEffect::Init (0xb422b0): generated switch plus the
        /// hand-translated parts (equality checks before the switches, label aliases and the
        /// rand() variants — rand() is consumed exactly like the client).
        /// </summary>
        public static StrFileEntry? ResolveStr(int id, bool minEffect, MsvcRand rand)
        {
            switch (id)
            {
                // if (id == X) checks ahead of the switch statements
                case 0x40: return new StrFileEntry("ArrowShot.str", -1);
                case 0x151: return new StrFileEntry("JobLvUP.str", -1);
                case 0x27b: return new StrFileEntry("fire dragon.str", -1);
                case 0x398: return new StrFileEntry("Firewall_per.str", -1);
                // goto aliases into other cases' labels
                case 0x9e: return ResolveStr(0x151, minEffect, rand);
                case 0xc3: return ResolveStr(0x17, minEffect, rand);
                case 0x13b: return ResolveStr(0xd, minEffect, rand);
                case 0x171: return ResolveStr(0x82, minEffect, rand);
                case 0x174: return ResolveStr(0x409, minEffect, rand);
                case 0x25b: return ResolveStr(0x31, minEffect, rand);
                case 0x2cd: return ResolveStr(0x29, minEffect, rand);
                case 0x2ee: return ResolveStr(0x99, minEffect, rand);
                case 0x2f4: return ResolveStr(0xdc, minEffect, rand);
                // rand() variants (rand() >= 0, so the sign fix-ups reduce to plain modulo)
                // (names from static tables: no string built per launch)
                case 0x19: return new StrFileEntry(FirewallStr[rand.Next() % 2], -1);
                case 0x31: return new StrFileEntry(FireHitStr[rand.Next() % 3], -1);
                case 0x34: return new StrFileEntry(WindHitStr[rand.Next() % 3], -1);
                case 0x5c: return new StrFileEntry(MeteorStr[rand.Next() % 3], -1);
                case 0x6d: return new StrFileEntry(BubbleStr[rand.Next() % 4], -1);
                case 0x8d: return new StrFileEntry(PneumaStr[rand.Next() % 3], -1);
                case 0xc7: return new StrFileEntry(PongStr[rand.Next() % 3], -1);
                case 0x3fc: return new StrFileEntry(Bubble1Str[rand.Next() % 4], -1);
                default: return StrFileFor(id, minEffect);
            }
        }

        public static int DefaultDuration(int id) => DefaultDurations.TryGetValue(id, out int d) ? d : 0;

        static readonly string[] FirewallStr = { "Firewall1.str", "Firewall2.str" };
        static readonly string[] FireHitStr = { "FireHit1.str", "FireHit2.str", "FireHit3.str" };
        static readonly string[] WindHitStr = { "WindHit1.str", "WindHit2.str", "WindHit3.str" };
        static readonly string[] MeteorStr = { "Meteor1.str", "Meteor2.str", "Meteor3.str" };
        static readonly string[] BubbleStr = { "Bubble1.str", "Bubble2.str", "Bubble3.str", "Bubble4.str" };
        static readonly string[] PneumaStr = { "Pneuma1.str", "Pneuma2.str", "Pneuma3.str" };
        static readonly string[] PongStr = { "Pong1.str", "Pong2.str", "Pong3.str" };
        static readonly string[] Bubble1Str = { "bubble1_1.str", "bubble2_1.str", "bubble3_1.str", "bubble4_1.str" };
    }
}
