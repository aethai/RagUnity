using System;

namespace Ragnarok.Data {
    /// <summary>
    /// How the client builds a garment's (robe's) sprite paths (RE: reference packages/robe/NOTES.md).
    /// <para>FUN_00ad3240 (the .act through FUN_00d13b50, the .spr through FUN_00d13bc0), called by the actor's robe draw
    /// 0xcc4c40: name = Lua ReqRobSprName(robe id) (RobeNameTable of SpriteRobeName); job = FUN_00d27fc0(body job, base
    /// job, true) into the job sprite name table; then</para>
    /// <para>.spr: <c>sprite\로브\{name}\{name}.spr</c> when that file exists, else <c>sprite\로브\{name}\{sex}\{job}_{sex}.spr</c>;
    /// .act: always <c>sprite\로브\{name}\{sex}\{job}_{sex}.act</c>. Both must exist or the robe is not drawn.</para>
    /// <para>The same job name table and job function build the body sprite path (0xad2a40), so a robe's per-job file
    /// carries the name of the body sprite it is drawn over. The doram use the shared .spr like everyone else (this client
    /// never asks for the *_doram.spr files newer GRFs carry).</para>
    /// </summary>
    public static class RoRobeSpriteRules {
        private const int HighJobOffset = 0xf6e; // 3950: jobs above it index the name table at job - 3950
        private const string NoviceSpriteName = "\uCD08\uBCF4\uC790";

        /// <summary>The job sprite name the client uses for a robe's (and the body's) per-job files.</summary>
        public static string ResolveJobSpriteName(int bodyJob, int baseJob) {
            int index = ResolveJobSpriteIndex(bodyJob, baseJob);
            string name = index >= 0 && index < JobSpriteNames.Length ? JobSpriteNames[index] : null;
            // the table is a vector of 0x6d95 entries filled with the novice name before the named slots are written
            return name ?? NoviceSpriteName;
        }

        /// <summary>
        /// 0xcc4c40: a female on a madogear (body jobs 4086 / 4087 / 4112) draws no robe.
        /// </summary>
        public static bool IsRobeHidden(int bodyJob, bool isMale) {
            return !isMale && (bodyJob == 4086 || bodyJob == 4087 || bodyJob == 4112);
        }

        /// <summary>
        /// The base paths (no extension) of a robe's .spr and .act, chosen like FUN_00ad3240: the shared
        /// <c>{name}/{name}.spr</c> when <paramref name="exists"/> finds it, else the per-job .spr next to the .act.
        /// </summary>
        public static bool TryResolveBasePaths(
            string robeFolder,
            string robeName,
            string sexSegment,
            int bodyJob,
            int baseJob,
            Func<string, bool> exists,
            out string spriteBasePath,
            out string actionBasePath
        ) {
            spriteBasePath = null;
            actionBasePath = null;
            if (string.IsNullOrEmpty(robeName) || string.IsNullOrEmpty(sexSegment)) {
                return false;
            }

            string root = $"data/sprite/{robeFolder}/{robeName}";
            actionBasePath = $"{root}/{sexSegment}/{ResolveJobSpriteName(bodyJob, baseJob)}_{sexSegment}";
            string shared = $"{root}/{robeName}";
            spriteBasePath = exists != null && exists(shared + ".spr") ? shared : actionBasePath;
            return true;
        }

        /// <summary>
        /// FUN_00d27fc0(bodyJob, job, true): the index into the job sprite name table. <paramref name="bodyJob"/> is the
        /// sprite job (mounts, costumes and all-riding applied, 0xbd0cd0), <paramref name="job"/> the actor's own job.
        /// </summary>
        public static int ResolveJobSpriteIndex(int bodyJob, int job) {
            switch (bodyJob) {
                case 22: // wedding
                case 26: // xmas
                case 27: // summer
                case 28: // hanbok
                case 29: // oktoberfest
                case 30: // summer 2
                    return bodyJob;
            }

            int kind;
            if (ToAllRidingJob(ToUnmountedJob(bodyJob)) == bodyJob) {
                kind = 1; // an all-riding body (or a job without one)
            } else if (IsPecoTypeMountJob(bodyJob)) {
                kind = 2;
            } else {
                kind = 0;
            }

            switch (job) {
                case 0x10ec: job = 0xfdc; break;
                case 0x10ed: job = 0xfe0; break;
                case 0x10ee: job = 0xfe1; break;
                case 0x10ef: job = 0xfdd; break;
                case 0x10f0: job = 0xfdf; break;
                case 0x10f1: job = 0xfde; break;
                case 0x10f2: job = 0xfe9; break;
                case 0x10f3: job = 0xfee; break;
                case 0x10f4: job = 0xfef; break;
                case 0x10f5: job = 0xfea; break;
                case 0x10f6: job = 0xfed; break;
                case 0x10f7: job = 0xfeb; break;
                case 0x10f8: job = 0xfec; break;
            }

            if (kind == 1) {
                job = ToAllRidingJob(job);
            } else if (kind == 2) {
                switch (job) {
                    case 0xfa8: job = 0xfae; break;
                    case 7: job = 0xd; break;
                    case 0xe: job = 0x15; break;
                    case 0xfaf: job = 0xfb6; break;
                    case 0xfbe: job = 0xfc4; break;
                    case 0xfc5: job = 0xfcc; break;
                    case 0xfd6: job = 0xff0; break;
                    case 0xfd8: job = 0xff4; break;
                    case 0xfdc: job = 0xff1; break;
                    case 0xfde: job = 0xff5; break;
                    case 0xfe2: job = 0xff2; break;
                    case 0xfe9: job = 0xff3; break;
                    case 0x1000: job = 0x100d; break;
                    case 0x1002: job = 0x100f; break;
                    case 0x1006: job = 0x100e; break;
                    case 0x109c: job = 0x10b8; break;
                    case 0x10a1: job = 0x10b6; break;
                    case 0x10a2: job = 0x10b9; break;
                }
            } else if (bodyJob == 0xff6 || bodyJob == 0xff7 || bodyJob == 0x1010 || bodyJob == 0x10b7) {
                // madogear bodies
                switch (job) {
                    case 0xfda: job = 0xff6; break;
                    case 0xfe0: job = 0xff7; break;
                    case 0x1004: job = 0x1010; break;
                    case 0x109d: job = 0x10b7; break;
                }
            }

            return job > HighJobOffset ? job - HighJobOffset : job;
        }

        /// <summary>The peco-type mount bodies of FUN_00d27fc0 (peco, dragon, gryphon, ...).</summary>
        private static bool IsPecoTypeMountJob(int bodyJob) {
            switch (bodyJob) {
                case 0xd:
                case 0x15:
                case 0xfae:
                case 0xfb6:
                case 0xfc4:
                case 0xfcc:
                case 0xff0:
                case 0xff1:
                case 0xff2:
                case 0xff3:
                case 0xff4:
                case 0xff5:
                case 0x100d:
                case 0x100e:
                case 0x100f:
                case 0x10b6:
                case 0x10b8:
                case 0x10b9:
                    return true;
                default:
                    return false;
            }
        }

        // session +0x4e80 (filled by 0xcf5bb0): 321 named entries, index = job (<= 3950) or job - 3950; every other slot is the novice name
        private static readonly string[] JobSpriteNames = {
            /*   0 */ "\uCD08\uBCF4\uC790",
            /*   1 */ "\uAC80\uC0AC",
            /*   2 */ "\uB9C8\uBC95\uC0AC",
            /*   3 */ "\uAD81\uC218",
            /*   4 */ "\uC131\uC9C1\uC790",
            /*   5 */ "\uC0C1\uC778",
            /*   6 */ "\uB3C4\uB451",
            /*   7 */ "\uAE30\uC0AC",
            /*   8 */ "\uD504\uB9AC\uC2A4\uD2B8",
            /*   9 */ "\uC704\uC800\uB4DC",
            /*  10 */ "\uC81C\uCCA0\uACF5",
            /*  11 */ "\uD5CC\uD130",
            /*  12 */ "\uC5B4\uC138\uC2E0",
            /*  13 */ "\uD398\uCF54\uD398\uCF54_\uAE30\uC0AC",
            /*  14 */ "\uD06C\uB8E8\uC138\uC774\uB354",
            /*  15 */ "\uBABD\uD06C",
            /*  16 */ "\uC138\uC774\uC9C0",
            /*  17 */ "\uB85C\uADF8",
            /*  18 */ "\uC5F0\uAE08\uC220\uC0AC",
            /*  19 */ "\uBC14\uB4DC",
            /*  20 */ "\uBB34\uD76C",
            /*  21 */ "\uC2E0\uD398\uCF54\uD06C\uB8E8\uC138\uC774\uB354",
            /*  22 */ "\uACB0\uD63C",
            /*  23 */ "\uC288\uD37C\uB178\uBE44\uC2A4",
            /*  24 */ "\uAC74\uB108",
            /*  25 */ "\uB2CC\uC790",
            /*  26 */ "\uC0B0\uD0C0",
            /*  27 */ "\uC5EC\uB984",
            /*  28 */ "\uD55C\uBCF5",
            /*  29 */ "\uC625\uD1A0\uBC84\uD328\uC2A4\uD2B8",
            /*  30 */ "\uC5EC\uB9842",
            /*  31 */ null,
            /*  32 */ null,
            /*  33 */ null,
            /*  34 */ null,
            /*  35 */ null,
            /*  36 */ null,
            /*  37 */ null,
            /*  38 */ null,
            /*  39 */ null,
            /*  40 */ null,
            /*  41 */ null,
            /*  42 */ null,
            /*  43 */ null,
            /*  44 */ null,
            /*  45 */ null,
            /*  46 */ null,
            /*  47 */ null,
            /*  48 */ null,
            /*  49 */ null,
            /*  50 */ null,
            /*  51 */ "\uCD08\uBCF4\uC790",
            /*  52 */ "\uAC80\uC0AC",
            /*  53 */ "\uB9C8\uBC95\uC0AC",
            /*  54 */ "\uAD81\uC218",
            /*  55 */ "\uC131\uC9C1\uC790",
            /*  56 */ "\uC0C1\uC778",
            /*  57 */ "\uB3C4\uB451",
            /*  58 */ "\uB85C\uB4DC\uB098\uC774\uD2B8",
            /*  59 */ "\uD558\uC774\uD504\uB9AC",
            /*  60 */ "\uD558\uC774\uC704\uC800\uB4DC",
            /*  61 */ "\uD654\uC774\uD2B8\uC2A4\uBBF8\uC2A4",
            /*  62 */ "\uC2A4\uB098\uC774\uD37C",
            /*  63 */ "\uC5B4\uC314\uC2E0\uD06C\uB85C\uC2A4",
            /*  64 */ "\uB85C\uB4DC\uD398\uCF54",
            /*  65 */ "\uD314\uB77C\uB518",
            /*  66 */ "\uCC54\uD53C\uC628",
            /*  67 */ "\uD504\uB85C\uD398\uC11C",
            /*  68 */ "\uC2A4\uD1A0\uCEE4",
            /*  69 */ "\uD06C\uB9AC\uC5D0\uC774\uD130",
            /*  70 */ "\uD074\uB77C\uC6B4",
            /*  71 */ "\uC9D1\uC2DC",
            /*  72 */ "\uD398\uCF54\uD314\uB77C\uB518",
            /*  73 */ "\uCD08\uBCF4\uC790",
            /*  74 */ "\uAC80\uC0AC",
            /*  75 */ "\uB9C8\uBC95\uC0AC",
            /*  76 */ "\uAD81\uC218",
            /*  77 */ "\uC131\uC9C1\uC790",
            /*  78 */ "\uC0C1\uC778",
            /*  79 */ "\uB3C4\uB451",
            /*  80 */ "\uAE30\uC0AC",
            /*  81 */ "\uD504\uB9AC\uC2A4\uD2B8",
            /*  82 */ "\uC704\uC800\uB4DC",
            /*  83 */ "\uC81C\uCCA0\uACF5",
            /*  84 */ "\uD5CC\uD130",
            /*  85 */ "\uC5B4\uC138\uC2E0",
            /*  86 */ "\uD398\uCF54\uD398\uCF54_\uAE30\uC0AC",
            /*  87 */ "\uD06C\uB8E8\uC138\uC774\uB354",
            /*  88 */ "\uBABD\uD06C",
            /*  89 */ "\uC138\uC774\uC9C0",
            /*  90 */ "\uB85C\uADF8",
            /*  91 */ "\uC5F0\uAE08\uC220\uC0AC",
            /*  92 */ "\uBC14\uB4DC",
            /*  93 */ "\uBB34\uD76C\uBC14\uC9C0",
            /*  94 */ "\uAD6C\uD398\uCF54\uD06C\uB8E8\uC138\uC774\uB354",
            /*  95 */ "\uC288\uD37C\uB178\uBE44\uC2A4",
            /*  96 */ "\uD0DC\uAD8C\uC18C\uB144",
            /*  97 */ "\uAD8C\uC131",
            /*  98 */ "\uAD8C\uC131\uC735\uD569",
            /*  99 */ "\uC18C\uC6B8\uB9C1\uCEE4",
            /* 100 */ "\uC131\uC9C1\uC790",
            /* 101 */ "\uAE30\uC0AC",
            /* 102 */ "\uC138\uC774\uC9C0",
            /* 103 */ null,
            /* 104 */ "\uB8EC\uB098\uC774\uD2B8",
            /* 105 */ "\uC6CC\uB85D",
            /* 106 */ "\uB808\uC778\uC838",
            /* 107 */ "\uC544\uD06C\uBE44\uC20D",
            /* 108 */ "\uBBF8\uCF00\uB2C9",
            /* 109 */ "\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4",
            /* 110 */ "\uB8EC\uB098\uC774\uD2B8",
            /* 111 */ "\uC6CC\uB85D",
            /* 112 */ "\uB808\uC778\uC838",
            /* 113 */ "\uC544\uD06C\uBE44\uC20D",
            /* 114 */ "\uBBF8\uCF00\uB2C9",
            /* 115 */ "\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4",
            /* 116 */ "\uAC00\uB4DC",
            /* 117 */ "\uC18C\uC11C\uB7EC",
            /* 118 */ "\uBBFC\uC2A4\uD2B8\uB7F4",
            /* 119 */ "\uC6D0\uB354\uB7EC",
            /* 120 */ "\uC288\uB77C",
            /* 121 */ "\uC81C\uB124\uB9AD",
            /* 122 */ "\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C",
            /* 123 */ "\uAC00\uB4DC",
            /* 124 */ "\uC18C\uC11C\uB7EC",
            /* 125 */ "\uBBFC\uC2A4\uD2B8\uB7F4",
            /* 126 */ "\uC6D0\uB354\uB7EC",
            /* 127 */ "\uC288\uB77C",
            /* 128 */ "\uC81C\uB124\uB9AD",
            /* 129 */ "\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C",
            /* 130 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB760",
            /* 131 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB760",
            /* 132 */ "\uADF8\uB9AC\uD3F0\uAC00\uB4DC",
            /* 133 */ "\uADF8\uB9AC\uD3F0\uAC00\uB4DC",
            /* 134 */ "\uB808\uC778\uC838\uB291\uB300",
            /* 135 */ "\uB808\uC778\uC838\uB291\uB300",
            /* 136 */ "\uB9C8\uB3C4\uAE30\uC5B4",
            /* 137 */ "\uB9C8\uB3C4\uAE30\uC5B4",
            /* 138 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7602",
            /* 139 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7602",
            /* 140 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7603",
            /* 141 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7603",
            /* 142 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7604",
            /* 143 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7604",
            /* 144 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7605",
            /* 145 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB7605",
            /* 146 */ "\uB8EC\uB098\uC774\uD2B8",
            /* 147 */ "\uC6CC\uB85D",
            /* 148 */ "\uB808\uC778\uC838",
            /* 149 */ "\uC544\uD06C\uBE44\uC20D",
            /* 150 */ "\uBBF8\uCF00\uB2C9",
            /* 151 */ "\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4",
            /* 152 */ "\uAC00\uB4DC",
            /* 153 */ "\uC18C\uC11C\uB7EC",
            /* 154 */ "\uBBFC\uC2A4\uD2B8\uB7F4",
            /* 155 */ "\uC6D0\uB354\uB7EC",
            /* 156 */ "\uC288\uB77C",
            /* 157 */ "\uC81C\uB124\uB9AD",
            /* 158 */ "\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C",
            /* 159 */ "\uB8EC\uB098\uC774\uD2B8\uC058\uB760",
            /* 160 */ "\uADF8\uB9AC\uD3F0\uAC00\uB4DC",
            /* 161 */ "\uB808\uC778\uC838\uB291\uB300",
            /* 162 */ "\uB9C8\uB3C4\uAE30\uC5B4",
            /* 163 */ null,
            /* 164 */ "\uB450\uAEBC\uBE44\uB2CC\uC790",
            /* 165 */ "\uD398\uCF54\uAC74\uB108",
            /* 166 */ "\uD398\uCF54\uAC80\uC0AC",
            /* 167 */ "\uB450\uAEBC\uBE44\uC18C\uC6B8\uB9C1\uCEE4",
            /* 168 */ "\uD654\uC774\uD2B8\uC2A4\uBBF8\uC2A4\uBA67\uB3FC\uC9C0",
            /* 169 */ "\uC0C1\uC778\uBA67\uB3FC\uC9C0",
            /* 170 */ "\uC81C\uB124\uB9AD\uBA67\uB3FC\uC9C0",
            /* 171 */ "\uD06C\uB9AC\uC5D0\uC774\uD130\uBA67\uB3FC\uC9C0",
            /* 172 */ "\uD0C0\uC870\uAD81\uC218",
            /* 173 */ "\uAD8C\uC131\uD3EC\uB9C1",
            /* 174 */ "\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 175 */ "\uBABD\uD06C\uC54C\uD30C\uCE74",
            /* 176 */ "\uBCF5\uC0AC\uC54C\uD30C\uCE74",
            /* 177 */ "\uC288\uB77C\uC54C\uD30C\uCE74",
            /* 178 */ "\uC288\uD37C\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 179 */ "\uC544\uD06C\uBE44\uC20D\uC54C\uD30C\uCE74",
            /* 180 */ "\uC5EC\uC6B0\uB9C8\uBC95\uC0AC",
            /* 181 */ "\uC5EC\uC6B0\uC138\uC774\uC9C0",
            /* 182 */ "\uC5EC\uC6B0\uC18C\uC11C\uB7EC",
            /* 183 */ "\uC5EC\uC6B0\uC6CC\uB85D",
            /* 184 */ "\uC5EC\uC6B0\uC704\uC800\uB4DC",
            /* 185 */ "\uC5EC\uC6B0\uD504\uB85C\uD398\uC11C",
            /* 186 */ "\uC5EC\uC6B0\uD558\uC774\uC704\uC800\uB4DC",
            /* 187 */ "\uC5F0\uAE08\uC220\uC0AC\uBA67\uB3FC\uC9C0",
            /* 188 */ "\uC81C\uCCA0\uACF5\uBA67\uB3FC\uC9C0",
            /* 189 */ "\uCC54\uD53C\uC628\uC54C\uD30C\uCE74",
            /* 190 */ "\uCF08\uBCA0\uB85C\uC2A4\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4",
            /* 191 */ "\uCF08\uBCA0\uB85C\uC2A4\uB3C4\uB451",
            /* 192 */ "\uCF08\uBCA0\uB85C\uC2A4\uB85C\uADF8",
            /* 193 */ "\uCF08\uBCA0\uB85C\uC2A4\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C",
            /* 194 */ "\uCF08\uBCA0\uB85C\uC2A4\uC2A4\uD1A0\uCEE4",
            /* 195 */ "\uCF08\uBCA0\uB85C\uC2A4\uC5B4\uC384\uC2E0",
            /* 196 */ "\uCF08\uBCA0\uB85C\uC2A4\uC5B4\uC384\uC2E0\uD06C\uB85C\uC2A4",
            /* 197 */ "\uD0C0\uC870\uBB34\uD76C",
            /* 198 */ "\uD0C0\uC870\uBBFC\uC2A4\uD2B8\uB7F4",
            /* 199 */ "\uD0C0\uC870\uBC14\uB4DC",
            /* 200 */ "\uD0C0\uC870\uC2A4\uB098\uC774\uD37C",
            /* 201 */ "\uD0C0\uC870\uC6D0\uB354\uB7EC",
            /* 202 */ "\uD0C0\uC870\uC9DA\uC2DC",
            /* 203 */ "\uD0C0\uC870\uD06C\uB77C\uC6B4",
            /* 204 */ "\uD0C0\uC870\uD5CC\uD130",
            /* 205 */ "\uD0DC\uAD8C\uC18C\uB144\uD3EC\uB9C1",
            /* 206 */ "\uD504\uB9AC\uC2A4\uD2B8\uC54C\uD30C\uCE74",
            /* 207 */ "\uD558\uC774\uD504\uB9AC\uC2A4\uD2B8\uC54C\uD30C\uCE74",
            /* 208 */ "\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 209 */ "\uD398\uCF54\uAC80\uC0AC",
            /* 210 */ "\uC5EC\uC6B0\uB9C8\uBC95\uC0AC",
            /* 211 */ "\uD0C0\uC870\uAD81\uC218",
            /* 212 */ "\uBCF5\uC0AC\uC54C\uD30C\uCE74",
            /* 213 */ "\uC0C1\uC778\uBA67\uB3FC\uC9C0",
            /* 214 */ "\uD0C0\uC870\uD5CC\uD130",
            /* 215 */ "\uCF08\uBCA0\uB85C\uC2A4\uC5B4\uC384\uC2E0",
            /* 216 */ "\uBABD\uD06C\uC54C\uD30C\uCE74",
            /* 217 */ "\uC5EC\uC6B0\uC138\uC774\uC9C0",
            /* 218 */ "\uCF08\uBCA0\uB85C\uC2A4\uB85C\uADF8",
            /* 219 */ "\uC5F0\uAE08\uC220\uC0AC\uBA67\uB3FC\uC9C0",
            /* 220 */ "\uD0C0\uC870\uBC14\uB4DC",
            /* 221 */ "dancer_b_riding",
            /* 222 */ "\uC288\uD37C\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 223 */ "\uC5EC\uC6B0\uC6CC\uB85D",
            /* 224 */ "\uC544\uD06C\uBE44\uC20D\uC54C\uD30C\uCE74",
            /* 225 */ "\uCF08\uBCA0\uB85C\uC2A4\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4",
            /* 226 */ "\uC5EC\uC6B0\uC18C\uC11C\uB7EC",
            /* 227 */ "\uD0C0\uC870\uBBFC\uC2A4\uD2B8\uB7F4",
            /* 228 */ "\uD0C0\uC870\uC6D0\uB354\uB7EC",
            /* 229 */ "\uC288\uB77C\uC54C\uD30C\uCE74",
            /* 230 */ "\uC81C\uB124\uB9AD\uBA67\uB3FC\uC9C0",
            /* 231 */ "\uCF08\uBCA0\uB85C\uC2A4\uB3C4\uB451",
            /* 232 */ "\uCF08\uBCA0\uB85C\uC2A4\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C",
            /* 233 */ "\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 234 */ "\uD398\uCF54\uAC80\uC0AC",
            /* 235 */ "\uC5EC\uC6B0\uB9C8\uBC95\uC0AC",
            /* 236 */ "\uD0C0\uC870\uAD81\uC218",
            /* 237 */ "\uBCF5\uC0AC\uC54C\uD30C\uCE74",
            /* 238 */ "\uC0C1\uC778\uBA67\uB3FC\uC9C0",
            /* 239 */ "\uCF08\uBCA0\uB85C\uC2A4\uB3C4\uB451",
            /* 240 */ "\uC288\uD37C\uB178\uBE44\uC2A4",
            /* 241 */ "\uC288\uD37C\uB178\uBE44\uC2A4",
            /* 242 */ "\uC288\uD37C\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 243 */ "\uC288\uD37C\uB178\uBE44\uC2A4\uD3EC\uB9C1",
            /* 244 */ "\uD504\uB9AC\uC2A4\uD2B8\uC54C\uD30C\uCE74",
            /* 245 */ "\uC5EC\uC6B0\uC704\uC800\uB4DC",
            /* 246 */ "\uC81C\uCCA0\uACF5\uBA67\uB3FC\uC9C0",
            /* 247 */ "\uBBF8\uCF00\uB2C9\uBA67\uB3FC\uC9C0",
            /* 248 */ "\uD0C0\uC870\uB808\uC778\uC838",
            /* 249 */ "\uC0AC\uC790\uAE30\uC0AC",
            /* 250 */ "\uC0AC\uC790\uB85C\uB4DC\uB098\uC774\uD2B8",
            /* 251 */ "\uC0AC\uC790\uB85C\uC584\uAC00\uB4DC",
            /* 252 */ "\uC0AC\uC790\uB8EC\uB098\uC774\uD2B8",
            /* 253 */ "\uC0AC\uC790\uD06C\uB8E8\uC138\uC774\uB354",
            /* 254 */ "\uC0AC\uC790\uD314\uB77C\uB518",
            /* 255 */ "\uBBF8\uCF00\uB2C9\uBA67\uB3FC\uC9C0",
            /* 256 */ "\uD0C0\uC870\uB808\uC778\uC838",
            /* 257 */ "\uC0AC\uC790\uAE30\uC0AC",
            /* 258 */ "\uC0AC\uC790\uB85C\uC584\uAC00\uB4DC",
            /* 259 */ "\uC0AC\uC790\uB8EC\uB098\uC774\uD2B8",
            /* 260 */ "\uC0AC\uC790\uD06C\uB8E8\uC138\uC774\uB354",
            /* 261 */ "kagerou",
            /* 262 */ "oboro",
            /* 263 */ "frog_kagerou",
            /* 264 */ "frog_oboro",
            /* 265 */ "rebellion",
            /* 266 */ "peco_rebellion",
            /* 267 */ null,
            /* 268 */ "summoner",
            /* 269 */ "cart_summoner",
            /* 270 */ "summoner",
            /* 271 */ "cart_summoner",
            /* 272 */ "\uB2CC\uC790",
            /* 273 */ "kagerou",
            /* 274 */ "oboro",
            /* 275 */ "\uD0DC\uAD8C\uC18C\uB144",
            /* 276 */ "\uAD8C\uC131",
            /* 277 */ "\uC18C\uC6B8\uB9C1\uCEE4",
            /* 278 */ "\uAC74\uB108",
            /* 279 */ "rebellion",
            /* 280 */ "\uB450\uAEBC\uBE44\uB2CC\uC790",
            /* 281 */ "frog_kagerou",
            /* 282 */ "frog_oboro",
            /* 283 */ "\uD0DC\uAD8C\uC18C\uB144\uD3EC\uB9C1",
            /* 284 */ "\uAD8C\uC131\uD3EC\uB9C1",
            /* 285 */ "\uB450\uAEBC\uBE44\uC18C\uC6B8\uB9C1\uCEE4",
            /* 286 */ "\uD398\uCF54\uAC74\uB108",
            /* 287 */ "peco_rebellion",
            /* 288 */ "\uAD8C\uC131\uC735\uD569",
            /* 289 */ "\uC131\uC81C",
            /* 290 */ "\uC18C\uC6B8\uB9AC\uD37C",
            /* 291 */ "\uC131\uC81C",
            /* 292 */ "\uC18C\uC6B8\uB9AC\uD37C",
            /* 293 */ "\uC131\uC81C\uC735\uD569",
            /* 294 */ "\uC131\uC81C\uC735\uD569",
            /* 295 */ "\uD574\uD0DC\uC131\uC81C",
            /* 296 */ "\uD574\uD0DC\uC18C\uC6B8\uB9AC\uD37C",
            /* 297 */ "\uD574\uD0DC\uC131\uC81C",
            /* 298 */ "\uD574\uD0DC\uC18C\uC6B8\uB9AC\uD37C",
            /* 299 */ null,
            /* 300 */ null,
            /* 301 */ null,
            /* 302 */ "DRAGON_KNIGHT",
            /* 303 */ "MEISTER",
            /* 304 */ "SHADOW_CROSS",
            /* 305 */ "ARCH_MAGE",
            /* 306 */ "CARDINAL",
            /* 307 */ "WINDHAWK",
            /* 308 */ "IMPERIAL_GUARD",
            /* 309 */ "BIOLO",
            /* 310 */ "ABYSS_CHASER",
            /* 311 */ "ELEMETAL_MASTER",
            /* 312 */ "INQUISITOR",
            /* 313 */ "TROUBADOUR",
            /* 314 */ "TROUVERE",
            /* 315 */ "DRAGON_KNIGHT_RIDING",
            /* 316 */ "MEISTER_RIDING",
            /* 317 */ "SHADOW_CROSS_RIDING",
            /* 318 */ "ARCH_MAGE_RIDING",
            /* 319 */ "CARDINAL_RIDING",
            /* 320 */ "WINDHAWK_RIDING",
            /* 321 */ "IMPERIAL_GUARD_RIDING",
            /* 322 */ "BIOLO_RIDING",
            /* 323 */ "ABYSS_CHASER_RIDING",
            /* 324 */ "ELEMETAL_MASTER_RIDING",
            /* 325 */ "INQUISITOR_RIDING",
            /* 326 */ "TROUBADOUR_RIDING",
            /* 327 */ "TROUVERE_RIDING",
            /* 328 */ "WOLF_WINDHAWK",
            /* 329 */ "MEISTER_MADOGEAR1",
            /* 330 */ "DRAGON_KNIGHT_CHICKEN",
            /* 331 */ "IMPERIAL_GUARD_CHICKEN",
            /* 332 */ null,
            /* 333 */ null,
            /* 334 */ null,
            /* 335 */ null,
            /* 336 */ null,
            /* 337 */ null,
            /* 338 */ null,
            /* 339 */ null,
            /* 340 */ null,
            /* 341 */ null,
            /* 342 */ null,
            /* 343 */ null,
            /* 344 */ null,
            /* 345 */ null,
            /* 346 */ null,
            /* 347 */ null,
            /* 348 */ null,
            /* 349 */ null,
            /* 350 */ null,
            /* 351 */ null,
            /* 352 */ "SKY_EMPEROR",
            /* 353 */ "SOUL_ASCETIC",
            /* 354 */ "SHINKIRO",
            /* 355 */ "SHIRANUI",
            /* 356 */ "NIGHT_WATCH",
            /* 357 */ "HYPER_NOVICE",
            /* 358 */ "SPIRIT_HANDLER",
            /* 359 */ "SKY_EMPEROR_RIDING",
            /* 360 */ "SOUL_ASCETIC_RIDING",
            /* 361 */ "SHINKIRO_RIDING",
            /* 362 */ "SHIRANUI_RIDING",
            /* 363 */ "NIGHT_WATCH_RIDING",
            /* 364 */ "HYPER_NOVICE_RIDING",
            /* 365 */ "SPIRIT_HANDLER_RIDING",
            /* 366 */ "SKY_EMPEROR2",
        };

        /// <summary>FUN_00d13230: a job to its all-riding (costume mount) job; other jobs unchanged.</summary>
        public static int ToAllRidingJob(int job) {
            switch (job) {
                case 0: return 4124;
                case 1: return 4116;
                case 2: return 4130;
                case 3: return 4122;
                case 4: return 4126;
                case 5: return 4119;
                case 6: return 4141;
                case 7: return 4199;
                case 8: return 4156;
                case 9: return 4134;
                case 10: return 4138;
                case 11: return 4154;
                case 12: return 4145;
                case 14: return 4203;
                case 15: return 4125;
                case 16: return 4131;
                case 17: return 4142;
                case 18: return 4137;
                case 19: return 4149;
                case 20: return 4147;
                case 23: return 4128;
                case 24: return 4115;
                case 25: return 4114;
                case 4001: return 4183;
                case 4002: return 4184;
                case 4003: return 4185;
                case 4004: return 4186;
                case 4005: return 4187;
                case 4006: return 4188;
                case 4007: return 4189;
                case 4008: return 4200;
                case 4009: return 4157;
                case 4010: return 4136;
                case 4011: return 4118;
                case 4012: return 4150;
                case 4013: return 4146;
                case 4015: return 4204;
                case 4016: return 4139;
                case 4017: return 4135;
                case 4018: return 4144;
                case 4019: return 4121;
                case 4020: return 4153;
                case 4021: return 4152;
                case 4023: return 4158;
                case 4024: return 4159;
                case 4025: return 4160;
                case 4026: return 4161;
                case 4027: return 4162;
                case 4028: return 4163;
                case 4029: return 4181;
                case 4030: return 4207;
                case 4031: return 4194;
                case 4032: return 4195;
                case 4033: return 4196;
                case 4034: return 4164;
                case 4035: return 4165;
                case 4037: return 4210;
                case 4038: return 4166;
                case 4039: return 4167;
                case 4040: return 4168;
                case 4041: return 4169;
                case 4042: return 4170;
                case 4043: return 4171;
                case 4045: return 4172;
                case 4046: return 4155;
                case 4047: case 4048: return 4123;
                case 4049: return 4117;
                case 4054: case 4060: return 4202;
                case 4055: case 4061: return 4133;
                case 4056: case 4062: return 4198;
                case 4057: case 4063: return 4129;
                case 4058: case 4064: return 4197;
                case 4059: case 4065: return 4140;
                case 4066: case 4073: return 4201;
                case 4067: case 4074: return 4132;
                case 4068: case 4075: return 4148;
                case 4069: case 4076: return 4151;
                case 4070: case 4077: return 4127;
                case 4071: case 4078: return 4120;
                case 4072: case 4079: return 4143;
                case 4096: return 4209;
                case 4097: return 4173;
                case 4098: return 4206;
                case 4099: return 4174;
                case 4100: return 4205;
                case 4101: return 4175;
                case 4102: return 4208;
                case 4103: return 4176;
                case 4104: return 4177;
                case 4105: return 4178;
                case 4106: return 4179;
                case 4107: return 4180;
                case 4108: return 4182;
                case 4190: return 4192;
                case 4191: return 4193;
                case 4211: return 4213;
                case 4212: return 4214;
                case 4215: return 4216;
                case 4218: return 4219;
                case 4220: return 4221;
                case 4222: return 4230;
                case 4223: return 4231;
                case 4224: return 4232;
                case 4225: return 4233;
                case 4226: case 4238: return 4234;
                case 4227: return 4235;
                case 4228: return 4236;
                case 4229: return 4237;
                case 4239: case 4243: return 4245;
                case 4240: return 4246;
                case 4241: case 4244: return 4247;
                case 4242: return 4248;
                case 4252: return 4265;
                case 4253: return 4266;
                case 4254: return 4267;
                case 4255: return 4268;
                case 4256: return 4269;
                case 4257: return 4270;
                case 4258: return 4271;
                case 4259: return 4272;
                case 4260: return 4273;
                case 4261: return 4274;
                case 4262: return 4275;
                case 4263: return 4276;
                case 4264: return 4277;
                case 4302: case 4316: return 4309;
                case 4303: return 4310;
                case 4304: return 4311;
                case 4305: return 4312;
                case 4306: return 4313;
                case 4307: return 4314;
                case 4308: return 4315;
                default: return job;
            }
        }

        /// <summary>FUN_00d28e90: a mounted / all-riding / dragon colour job to its base job; other jobs unchanged.</summary>
        public static int ToUnmountedJob(int job) {
            switch (job) {
                case 13: case 4199: return 7;
                case 21: case 4203: return 14;
                case 4014: case 4200: return 4008;
                case 4022: case 4204: return 4015;
                case 4036: case 4207: return 4030;
                case 4044: case 4210: return 4037;
                case 4080: case 4088: case 4090: case 4092: case 4094: case 4202: return 4054;
                case 4081: case 4089: case 4091: case 4093: case 4095: return 4060;
                case 4082: case 4201: return 4066;
                case 4083: return 4073;
                case 4084: case 4198: return 4056;
                case 4085: return 4062;
                case 4086: case 4197: return 4058;
                case 4087: return 4064;
                case 4109: case 4209: return 4096;
                case 4110: case 4208: return 4102;
                case 4111: case 4206: return 4098;
                case 4112: case 4205: return 4100;
                case 4114: return 25;
                case 4115: return 24;
                case 4116: return 1;
                case 4117: return 4049;
                case 4118: return 4011;
                case 4119: return 5;
                case 4120: return 4071;
                case 4121: return 4019;
                case 4122: return 3;
                case 4123: return 4047;
                case 4124: return 0;
                case 4125: return 15;
                case 4126: return 4;
                case 4127: return 4070;
                case 4128: return 23;
                case 4129: return 4057;
                case 4130: return 2;
                case 4131: return 16;
                case 4132: return 4067;
                case 4133: return 4055;
                case 4134: return 9;
                case 4135: return 4017;
                case 4136: return 4010;
                case 4137: return 18;
                case 4138: return 10;
                case 4139: return 4016;
                case 4140: return 4059;
                case 4141: return 6;
                case 4142: return 17;
                case 4143: return 4072;
                case 4144: return 4018;
                case 4145: return 12;
                case 4146: return 4013;
                case 4147: return 20;
                case 4148: return 4068;
                case 4149: return 19;
                case 4150: return 4012;
                case 4151: return 4069;
                case 4152: return 4021;
                case 4153: return 4020;
                case 4154: return 11;
                case 4155: return 4046;
                case 4156: return 8;
                case 4157: return 4009;
                case 4158: return 4023;
                case 4159: return 4024;
                case 4160: return 4025;
                case 4161: return 4026;
                case 4162: return 4027;
                case 4163: return 4028;
                case 4164: return 4034;
                case 4165: return 4035;
                case 4166: return 4038;
                case 4167: return 4039;
                case 4168: return 4040;
                case 4169: return 4041;
                case 4170: return 4042;
                case 4171: return 4043;
                case 4172: return 4045;
                case 4173: return 4097;
                case 4174: return 4099;
                case 4175: return 4101;
                case 4176: return 4103;
                case 4177: return 4104;
                case 4178: return 4105;
                case 4179: return 4106;
                case 4180: return 4107;
                case 4181: return 4029;
                case 4182: return 4108;
                case 4183: return 4001;
                case 4184: return 4002;
                case 4185: return 4003;
                case 4186: return 4004;
                case 4187: return 4005;
                case 4188: return 4006;
                case 4189: return 4007;
                case 4192: return 4190;
                case 4193: return 4191;
                case 4194: return 4031;
                case 4195: return 4032;
                case 4196: return 4033;
                case 4213: case 4231: return 4211;
                case 4214: return 4212;
                case 4216: return 4215;
                case 4219: return 4218;
                case 4221: return 4220;
                case 4230: return 4222;
                case 4232: return 4224;
                case 4233: return 4225;
                case 4234: return 4226;
                case 4235: return 4227;
                case 4236: return 4228;
                case 4237: return 4229;
                case 4245: return 4239;
                case 4246: return 4240;
                case 4247: return 4241;
                case 4248: return 4242;
                case 4265: case 4280: return 4252;
                case 4266: case 4279: return 4253;
                case 4267: return 4254;
                case 4268: return 4255;
                case 4269: return 4256;
                case 4270: case 4278: return 4257;
                case 4271: case 4281: return 4258;
                case 4272: return 4259;
                case 4273: return 4260;
                case 4274: return 4261;
                case 4275: return 4262;
                case 4276: return 4263;
                case 4277: return 4264;
                case 4309: return 4302;
                case 4310: return 4303;
                case 4311: return 4304;
                case 4312: return 4305;
                case 4313: return 4306;
                case 4314: return 4307;
                case 4315: return 4308;
                default: return job;
            }
        }
    }
}
