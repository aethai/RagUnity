// Generated from reference out/skillfx/tbl_actionfx.tsv (FUN_00d15360) and tbl_begincast.tsv (FUN_00d13d20), emulated per
// skill id against game.exe.
// Do not edit by hand.
namespace Ragnarok.Combat {
    public static partial class RoOriginalCombatRules {
        /// <summary>
        /// The caster's SetAction state for a skill action (ZC_NOTIFY_SKILL / ZC_USE_SKILL, FUN_00d15360): 7 the skill action
        /// (ACT action 12 for most jobs), 2 / 9 an attack motion, 12 the throw (ACT action 5), 5 pick up, 0 stand, −1 no
        /// SetAction (the current motion is kept); 0x10..0x33 are scripted motions (FUN_00be5f40).
        /// </summary>
        public static int SkillActionState(int skillId) {
            switch (skillId) {
                case 446: case 478: case 490: case 2028: case 2215: case 2218: case 2219: case 2220:
                case 2221: case 2240: case 2241: case 2242: case 2263: case 2264: case 2265: case 2415:
                case 2416: case 2481: case 2482: case 2484: case 2485: case 2486: case 2490: case 2516:
                case 2520: case 5202: case 5219: case 5223: case 5224: case 5226: case 5231: case 5236:
                case 5243: case 5244: return -1;
                case 29: case 151: case 196: case 209: case 249: case 252: case 256: case 257:
                case 261: case 268: case 269: case 304: case 349: case 350: case 356: case 369:
                case 380: case 384: case 387: case 401: case 411: case 444: case 690: case 730:
                case 1015: case 2334: case 5209: case 8204: case 8219: case 8220: return 0;
                case 364: return 1;
                case 5: case 7: case 42: case 46: case 47: case 56: case 57: case 58:
                case 61: case 62: case 109: case 212: case 214: case 219: case 253: case 263:
                case 266: case 267: case 338: case 366: case 367: case 368: case 370: case 372:
                case 379: case 382: case 397: case 398: case 399: case 406: case 484: case 499:
                case 502: case 503: case 512: case 513: case 514: case 515: case 518: case 519:
                case 520: case 661: case 674: case 712: case 728: case 732: case 740: case 1001:
                case 1005: case 1009: case 1016: case 2002: case 2004: case 2005: case 2006: case 2017:
                case 2022: case 2023: case 2029: case 2030: case 2031: case 2036: case 2037: case 2233:
                case 2236: case 2259: case 2261: case 2279: case 2280: case 2284: case 2288: case 2298:
                case 2314: case 2317: case 2320: case 2324: case 2342: case 2476: case 5204: case 5206:
                case 5208: case 5211: case 5213: case 5245: case 5248: case 5249: case 5250: case 5255:
                case 5263: case 5264: case 5266: case 5267: case 5277: case 5283: case 5286: case 5287:
                case 5289: case 5291: case 5292: case 5294: case 5295: case 5296: case 5314: case 5315:
                case 5316: case 5320: case 5322: case 5344: case 5345: case 5346: case 5348: case 5382:
                case 8202: case 8601: case 8602: case 8603: case 8604: case 8605: return 2;
                case 115: case 116: case 117: case 118: case 119: case 120: case 121: case 122:
                case 123: case 124: case 125: case 688: case 1013: case 2238: case 2239: case 2249:
                case 2250: case 2251: case 2252: case 2253: case 2254: case 2267: case 5335: return 5;
                case 316: case 324: case 394: case 530: case 2258: case 2307: case 2308: case 2312:
                case 3004: case 5330: case 5334: case 5355: return 9;
                case 707: case 2003: case 2011: return 11;
                case 59: case 149: case 152: case 229: case 230: case 231: case 232: case 250:
                case 251: case 480: case 1004: case 2278: case 2480: case 2493: case 5340: case 5341:
                case 5342: case 5343: return 12;
                case 306: case 307: case 308: case 309: case 310: case 311: case 312: case 313:
                case 325: case 327: case 328: case 329: case 330: case 395: case 396: case 488:
                case 1011: return 16;
                case 317: case 319: case 320: case 321: case 322: return 17;
                case 2327: case 2330: return 18;
                case 426: case 2581: case 5023: return 20;
                case 412: case 2015: case 2285: return 21;
                case 414: case 2478: return 22;
                case 416: case 445: case 447: case 448: case 449: case 450: case 451: case 452:
                case 453: case 454: case 455: case 456: case 457: case 458: case 460: case 461:
                case 494: case 572: case 573: case 574: case 575: case 2333: case 2340: case 2596:
                case 2597: case 2598: case 2599: return 23;
                case 418: case 2018: case 2343: return 24;
                case 420: case 2269: case 2323: return 25;
                case 419: return 26;
                case 421: case 2336: return 27;
                case 413: case 2337: case 2576: return 30;
                case 415: case 2313: return 31;
                case 417: case 2311: case 2344: case 2345: case 2346: case 2347: case 2348: case 2593: return 32;
                case 431: case 432: case 433: return 33;
                case 428: case 429: case 430: return 34;
                case 527: case 528: case 529: case 532: case 535: case 536: case 537: case 538: return 35;
                case 523: case 524: case 525: case 526: case 531: case 534: case 539: case 540:
                case 541: case 542: case 543: case 3005: case 3006: case 3007: case 3008: case 3009:
                case 3020: case 3022: case 3023: return 36;
                case 3011: case 3013: case 3014: case 3015: case 3016: case 3017: case 3018: case 3021:
                case 3026: case 3027: case 3029: return 37;
                case 501: case 504: case 505: case 506: case 516: case 517: return 38;
                case 500: case 2256: case 2257: case 2260: case 2271: case 2272: case 2445: return 39;
                case 508: case 521: return 40;
                case 2268: case 2273: case 2274: case 2275: return 43;
                case 2270: return 44;
                case 5021: return 49;
                case 2580: return 51;
                default: return 7;
            }
        }

        /// <summary>
        /// The caster's SetAction state when a cast starts (ZC_USESKILL_ACK with a cast time, FUN_00d13d20, job 0 rows): 8 the
        /// cast pose (ACT action 12, frame 0 held) for most skills, 2 / 9 attack, 5 pick up, 12 throw, 11 the ready pose once,
        /// 0 stand, 7 the skill action, −1 none; other values are scripted motions (FUN_00be5f40).
        /// </summary>
        public static int CastStartState(int skillId) {
            switch (skillId) {
                case 735: case 736: case 739: case 742: case 744: case 745: case 2241: case 2242:
                case 2256: case 2258: case 2260: case 2262: case 2267: case 2268: case 2270: case 2271:
                case 2272: case 2273: case 2274: case 2275: case 2326: case 2327: case 2328: case 2329:
                case 2330: case 2332: case 2338: case 2340: case 2341: case 2343: case 2344: case 2345:
                case 2346: case 2347: case 2348: case 2443: case 2444: case 2446: case 2447: case 2448:
                case 2449: case 2450: case 2451: case 2452: case 2453: case 2455: case 2456: case 2457:
                case 2458: case 2459: case 2460: case 2476: case 2477: case 2478: case 2480: case 2492:
                case 2494: case 2495: case 2496: case 2497: case 2517: case 2518: case 2520: case 5321: return -1;
                case 34: case 196: case 209: case 230: case 231: case 232: case 234: case 235:
                case 236: case 237: case 243: case 244: case 247: case 249: case 268: case 270:
                case 349: case 389: case 425: case 475: case 479: case 493: case 555: case 689:
                case 1001: case 1010: case 1014: case 2290: case 2304: case 8220: return 0;
                case 5: case 7: case 42: case 46: case 56: case 57: case 58: case 62:
                case 110: case 499: case 1009: case 2002: case 2055: case 2259: case 2261: case 2280:
                case 5203: case 8202: return 2;
                case 115: case 116: case 117: case 118: case 119: case 120: case 121: case 122:
                case 123: case 124: case 125: case 151: case 688: case 2239: case 2253: case 2254: return 5;
                case 29: case 136: case 496: case 497: case 498: case 690: case 708: case 720:
                case 728: case 737: case 2022: case 2056: case 2201: case 2202: case 2203: case 2204:
                case 2205: case 2206: case 2207: case 2209: case 2210: case 2211: case 2212: case 2213:
                case 2214: case 2216: case 2217: case 2222: case 2223: case 2224: case 2229: case 2230:
                case 2231: case 2233: case 2236: case 2237: case 2240: case 2243: case 2244: case 2246:
                case 2286: case 2287: case 2292: case 2293: case 2294: case 2295: case 2296: case 2297:
                case 2298: case 2313: case 2314: case 2315: case 2316: case 2317: case 2318: case 2319:
                case 2320: case 2321: case 2322: case 2323: case 2324: case 2325: case 2454: case 2479:
                case 2482: case 2483: case 2486: case 2490: case 5078: case 8401: case 8425: case 8426:
                case 8428: case 8430: case 8431: case 8433: case 8434: case 8435: case 8442: return 7;
                case 61: case 149: case 152: case 397: case 1004: return 11;
                case 59: case 446: case 2481: case 2485: return 12;
                case 1011: case 2350: case 2351: case 2352: case 2381: case 2382: case 2383: case 2413:
                case 2414: case 2417: case 2418: case 2419: case 2420: case 2421: case 2422: case 2423:
                case 2424: case 2425: case 2426: case 2427: case 2428: case 2429: case 2430: case 2431:
                case 2432: case 2433: case 2434: return 16;
                case 47: case 382: case 394: case 2288: return 18;
                case 426: case 2581: case 5023: return 19;
                case 478: case 490: return 28;
                case 3006: case 3009: return 36;
                case 525: case 532: case 534: case 535: case 536: case 537: case 538: case 539:
                case 540: case 541: case 542: case 543: case 3011: case 3013: case 3014: case 3015:
                case 3016: case 3017: case 3018: case 3021: case 3026: case 3027: case 3029: return 37;
                case 503: case 504: case 505: case 512: case 513: case 514: case 515: case 517: return 41;
                case 518: case 519: case 520: case 521: return 42;
                case 2564: return 46;
                case 5026: case 5027: case 5032: case 5033: case 5035: case 5036: case 5038: case 5040:
                case 5043: return 50;
                default: return 8;
            }
        }
    }
}
