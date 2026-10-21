namespace KeystoneHarmony
{
    // Made by a script: one small class for each slot, each mod having a slot of its own, so that the timing that is
    // put round every measured method knows whose it is without looking anything up (see Clock). 256 since 2026-10-18:
    // there were 64, and in a game of 68 mods every mod past the 62nd was put down to the 62nd.
    static class Slots
    {
        public const int Count = 256;
        public static readonly System.Type[] Each =
        {
            typeof(S00), typeof(S01), typeof(S02), typeof(S03), typeof(S04), typeof(S05), typeof(S06), typeof(S07),
            typeof(S08), typeof(S09), typeof(S10), typeof(S11), typeof(S12), typeof(S13), typeof(S14), typeof(S15),
            typeof(S16), typeof(S17), typeof(S18), typeof(S19), typeof(S20), typeof(S21), typeof(S22), typeof(S23),
            typeof(S24), typeof(S25), typeof(S26), typeof(S27), typeof(S28), typeof(S29), typeof(S30), typeof(S31),
            typeof(S32), typeof(S33), typeof(S34), typeof(S35), typeof(S36), typeof(S37), typeof(S38), typeof(S39),
            typeof(S40), typeof(S41), typeof(S42), typeof(S43), typeof(S44), typeof(S45), typeof(S46), typeof(S47),
            typeof(S48), typeof(S49), typeof(S50), typeof(S51), typeof(S52), typeof(S53), typeof(S54), typeof(S55),
            typeof(S56), typeof(S57), typeof(S58), typeof(S59), typeof(S60), typeof(S61), typeof(S62), typeof(S63),
            typeof(S64), typeof(S65), typeof(S66), typeof(S67), typeof(S68), typeof(S69), typeof(S70), typeof(S71),
            typeof(S72), typeof(S73), typeof(S74), typeof(S75), typeof(S76), typeof(S77), typeof(S78), typeof(S79),
            typeof(S80), typeof(S81), typeof(S82), typeof(S83), typeof(S84), typeof(S85), typeof(S86), typeof(S87),
            typeof(S88), typeof(S89), typeof(S90), typeof(S91), typeof(S92), typeof(S93), typeof(S94), typeof(S95),
            typeof(S96), typeof(S97), typeof(S98), typeof(S99), typeof(S100), typeof(S101), typeof(S102), typeof(S103),
            typeof(S104), typeof(S105), typeof(S106), typeof(S107), typeof(S108), typeof(S109), typeof(S110), typeof(S111),
            typeof(S112), typeof(S113), typeof(S114), typeof(S115), typeof(S116), typeof(S117), typeof(S118), typeof(S119),
            typeof(S120), typeof(S121), typeof(S122), typeof(S123), typeof(S124), typeof(S125), typeof(S126), typeof(S127),
            typeof(S128), typeof(S129), typeof(S130), typeof(S131), typeof(S132), typeof(S133), typeof(S134), typeof(S135),
            typeof(S136), typeof(S137), typeof(S138), typeof(S139), typeof(S140), typeof(S141), typeof(S142), typeof(S143),
            typeof(S144), typeof(S145), typeof(S146), typeof(S147), typeof(S148), typeof(S149), typeof(S150), typeof(S151),
            typeof(S152), typeof(S153), typeof(S154), typeof(S155), typeof(S156), typeof(S157), typeof(S158), typeof(S159),
            typeof(S160), typeof(S161), typeof(S162), typeof(S163), typeof(S164), typeof(S165), typeof(S166), typeof(S167),
            typeof(S168), typeof(S169), typeof(S170), typeof(S171), typeof(S172), typeof(S173), typeof(S174), typeof(S175),
            typeof(S176), typeof(S177), typeof(S178), typeof(S179), typeof(S180), typeof(S181), typeof(S182), typeof(S183),
            typeof(S184), typeof(S185), typeof(S186), typeof(S187), typeof(S188), typeof(S189), typeof(S190), typeof(S191),
            typeof(S192), typeof(S193), typeof(S194), typeof(S195), typeof(S196), typeof(S197), typeof(S198), typeof(S199),
            typeof(S200), typeof(S201), typeof(S202), typeof(S203), typeof(S204), typeof(S205), typeof(S206), typeof(S207),
            typeof(S208), typeof(S209), typeof(S210), typeof(S211), typeof(S212), typeof(S213), typeof(S214), typeof(S215),
            typeof(S216), typeof(S217), typeof(S218), typeof(S219), typeof(S220), typeof(S221), typeof(S222), typeof(S223),
            typeof(S224), typeof(S225), typeof(S226), typeof(S227), typeof(S228), typeof(S229), typeof(S230), typeof(S231),
            typeof(S232), typeof(S233), typeof(S234), typeof(S235), typeof(S236), typeof(S237), typeof(S238), typeof(S239),
            typeof(S240), typeof(S241), typeof(S242), typeof(S243), typeof(S244), typeof(S245), typeof(S246), typeof(S247),
            typeof(S248), typeof(S249), typeof(S250), typeof(S251), typeof(S252), typeof(S253), typeof(S254), typeof(S255),
        };
    }

    // (Finalizer and not Postfix: it runs even when the method throws, so the stack of what is running stays true)
    static class S00 { public static void Prefix() => Clock.Enter(0); public static void Finalizer() => Clock.Leave(0); }
    static class S01 { public static void Prefix() => Clock.Enter(1); public static void Finalizer() => Clock.Leave(1); }
    static class S02 { public static void Prefix() => Clock.Enter(2); public static void Finalizer() => Clock.Leave(2); }
    static class S03 { public static void Prefix() => Clock.Enter(3); public static void Finalizer() => Clock.Leave(3); }
    static class S04 { public static void Prefix() => Clock.Enter(4); public static void Finalizer() => Clock.Leave(4); }
    static class S05 { public static void Prefix() => Clock.Enter(5); public static void Finalizer() => Clock.Leave(5); }
    static class S06 { public static void Prefix() => Clock.Enter(6); public static void Finalizer() => Clock.Leave(6); }
    static class S07 { public static void Prefix() => Clock.Enter(7); public static void Finalizer() => Clock.Leave(7); }
    static class S08 { public static void Prefix() => Clock.Enter(8); public static void Finalizer() => Clock.Leave(8); }
    static class S09 { public static void Prefix() => Clock.Enter(9); public static void Finalizer() => Clock.Leave(9); }
    static class S10 { public static void Prefix() => Clock.Enter(10); public static void Finalizer() => Clock.Leave(10); }
    static class S11 { public static void Prefix() => Clock.Enter(11); public static void Finalizer() => Clock.Leave(11); }
    static class S12 { public static void Prefix() => Clock.Enter(12); public static void Finalizer() => Clock.Leave(12); }
    static class S13 { public static void Prefix() => Clock.Enter(13); public static void Finalizer() => Clock.Leave(13); }
    static class S14 { public static void Prefix() => Clock.Enter(14); public static void Finalizer() => Clock.Leave(14); }
    static class S15 { public static void Prefix() => Clock.Enter(15); public static void Finalizer() => Clock.Leave(15); }
    static class S16 { public static void Prefix() => Clock.Enter(16); public static void Finalizer() => Clock.Leave(16); }
    static class S17 { public static void Prefix() => Clock.Enter(17); public static void Finalizer() => Clock.Leave(17); }
    static class S18 { public static void Prefix() => Clock.Enter(18); public static void Finalizer() => Clock.Leave(18); }
    static class S19 { public static void Prefix() => Clock.Enter(19); public static void Finalizer() => Clock.Leave(19); }
    static class S20 { public static void Prefix() => Clock.Enter(20); public static void Finalizer() => Clock.Leave(20); }
    static class S21 { public static void Prefix() => Clock.Enter(21); public static void Finalizer() => Clock.Leave(21); }
    static class S22 { public static void Prefix() => Clock.Enter(22); public static void Finalizer() => Clock.Leave(22); }
    static class S23 { public static void Prefix() => Clock.Enter(23); public static void Finalizer() => Clock.Leave(23); }
    static class S24 { public static void Prefix() => Clock.Enter(24); public static void Finalizer() => Clock.Leave(24); }
    static class S25 { public static void Prefix() => Clock.Enter(25); public static void Finalizer() => Clock.Leave(25); }
    static class S26 { public static void Prefix() => Clock.Enter(26); public static void Finalizer() => Clock.Leave(26); }
    static class S27 { public static void Prefix() => Clock.Enter(27); public static void Finalizer() => Clock.Leave(27); }
    static class S28 { public static void Prefix() => Clock.Enter(28); public static void Finalizer() => Clock.Leave(28); }
    static class S29 { public static void Prefix() => Clock.Enter(29); public static void Finalizer() => Clock.Leave(29); }
    static class S30 { public static void Prefix() => Clock.Enter(30); public static void Finalizer() => Clock.Leave(30); }
    static class S31 { public static void Prefix() => Clock.Enter(31); public static void Finalizer() => Clock.Leave(31); }
    static class S32 { public static void Prefix() => Clock.Enter(32); public static void Finalizer() => Clock.Leave(32); }
    static class S33 { public static void Prefix() => Clock.Enter(33); public static void Finalizer() => Clock.Leave(33); }
    static class S34 { public static void Prefix() => Clock.Enter(34); public static void Finalizer() => Clock.Leave(34); }
    static class S35 { public static void Prefix() => Clock.Enter(35); public static void Finalizer() => Clock.Leave(35); }
    static class S36 { public static void Prefix() => Clock.Enter(36); public static void Finalizer() => Clock.Leave(36); }
    static class S37 { public static void Prefix() => Clock.Enter(37); public static void Finalizer() => Clock.Leave(37); }
    static class S38 { public static void Prefix() => Clock.Enter(38); public static void Finalizer() => Clock.Leave(38); }
    static class S39 { public static void Prefix() => Clock.Enter(39); public static void Finalizer() => Clock.Leave(39); }
    static class S40 { public static void Prefix() => Clock.Enter(40); public static void Finalizer() => Clock.Leave(40); }
    static class S41 { public static void Prefix() => Clock.Enter(41); public static void Finalizer() => Clock.Leave(41); }
    static class S42 { public static void Prefix() => Clock.Enter(42); public static void Finalizer() => Clock.Leave(42); }
    static class S43 { public static void Prefix() => Clock.Enter(43); public static void Finalizer() => Clock.Leave(43); }
    static class S44 { public static void Prefix() => Clock.Enter(44); public static void Finalizer() => Clock.Leave(44); }
    static class S45 { public static void Prefix() => Clock.Enter(45); public static void Finalizer() => Clock.Leave(45); }
    static class S46 { public static void Prefix() => Clock.Enter(46); public static void Finalizer() => Clock.Leave(46); }
    static class S47 { public static void Prefix() => Clock.Enter(47); public static void Finalizer() => Clock.Leave(47); }
    static class S48 { public static void Prefix() => Clock.Enter(48); public static void Finalizer() => Clock.Leave(48); }
    static class S49 { public static void Prefix() => Clock.Enter(49); public static void Finalizer() => Clock.Leave(49); }
    static class S50 { public static void Prefix() => Clock.Enter(50); public static void Finalizer() => Clock.Leave(50); }
    static class S51 { public static void Prefix() => Clock.Enter(51); public static void Finalizer() => Clock.Leave(51); }
    static class S52 { public static void Prefix() => Clock.Enter(52); public static void Finalizer() => Clock.Leave(52); }
    static class S53 { public static void Prefix() => Clock.Enter(53); public static void Finalizer() => Clock.Leave(53); }
    static class S54 { public static void Prefix() => Clock.Enter(54); public static void Finalizer() => Clock.Leave(54); }
    static class S55 { public static void Prefix() => Clock.Enter(55); public static void Finalizer() => Clock.Leave(55); }
    static class S56 { public static void Prefix() => Clock.Enter(56); public static void Finalizer() => Clock.Leave(56); }
    static class S57 { public static void Prefix() => Clock.Enter(57); public static void Finalizer() => Clock.Leave(57); }
    static class S58 { public static void Prefix() => Clock.Enter(58); public static void Finalizer() => Clock.Leave(58); }
    static class S59 { public static void Prefix() => Clock.Enter(59); public static void Finalizer() => Clock.Leave(59); }
    static class S60 { public static void Prefix() => Clock.Enter(60); public static void Finalizer() => Clock.Leave(60); }
    static class S61 { public static void Prefix() => Clock.Enter(61); public static void Finalizer() => Clock.Leave(61); }
    static class S62 { public static void Prefix() => Clock.Enter(62); public static void Finalizer() => Clock.Leave(62); }
    static class S63 { public static void Prefix() => Clock.Enter(63); public static void Finalizer() => Clock.Leave(63); }
    static class S64 { public static void Prefix() => Clock.Enter(64); public static void Finalizer() => Clock.Leave(64); }
    static class S65 { public static void Prefix() => Clock.Enter(65); public static void Finalizer() => Clock.Leave(65); }
    static class S66 { public static void Prefix() => Clock.Enter(66); public static void Finalizer() => Clock.Leave(66); }
    static class S67 { public static void Prefix() => Clock.Enter(67); public static void Finalizer() => Clock.Leave(67); }
    static class S68 { public static void Prefix() => Clock.Enter(68); public static void Finalizer() => Clock.Leave(68); }
    static class S69 { public static void Prefix() => Clock.Enter(69); public static void Finalizer() => Clock.Leave(69); }
    static class S70 { public static void Prefix() => Clock.Enter(70); public static void Finalizer() => Clock.Leave(70); }
    static class S71 { public static void Prefix() => Clock.Enter(71); public static void Finalizer() => Clock.Leave(71); }
    static class S72 { public static void Prefix() => Clock.Enter(72); public static void Finalizer() => Clock.Leave(72); }
    static class S73 { public static void Prefix() => Clock.Enter(73); public static void Finalizer() => Clock.Leave(73); }
    static class S74 { public static void Prefix() => Clock.Enter(74); public static void Finalizer() => Clock.Leave(74); }
    static class S75 { public static void Prefix() => Clock.Enter(75); public static void Finalizer() => Clock.Leave(75); }
    static class S76 { public static void Prefix() => Clock.Enter(76); public static void Finalizer() => Clock.Leave(76); }
    static class S77 { public static void Prefix() => Clock.Enter(77); public static void Finalizer() => Clock.Leave(77); }
    static class S78 { public static void Prefix() => Clock.Enter(78); public static void Finalizer() => Clock.Leave(78); }
    static class S79 { public static void Prefix() => Clock.Enter(79); public static void Finalizer() => Clock.Leave(79); }
    static class S80 { public static void Prefix() => Clock.Enter(80); public static void Finalizer() => Clock.Leave(80); }
    static class S81 { public static void Prefix() => Clock.Enter(81); public static void Finalizer() => Clock.Leave(81); }
    static class S82 { public static void Prefix() => Clock.Enter(82); public static void Finalizer() => Clock.Leave(82); }
    static class S83 { public static void Prefix() => Clock.Enter(83); public static void Finalizer() => Clock.Leave(83); }
    static class S84 { public static void Prefix() => Clock.Enter(84); public static void Finalizer() => Clock.Leave(84); }
    static class S85 { public static void Prefix() => Clock.Enter(85); public static void Finalizer() => Clock.Leave(85); }
    static class S86 { public static void Prefix() => Clock.Enter(86); public static void Finalizer() => Clock.Leave(86); }
    static class S87 { public static void Prefix() => Clock.Enter(87); public static void Finalizer() => Clock.Leave(87); }
    static class S88 { public static void Prefix() => Clock.Enter(88); public static void Finalizer() => Clock.Leave(88); }
    static class S89 { public static void Prefix() => Clock.Enter(89); public static void Finalizer() => Clock.Leave(89); }
    static class S90 { public static void Prefix() => Clock.Enter(90); public static void Finalizer() => Clock.Leave(90); }
    static class S91 { public static void Prefix() => Clock.Enter(91); public static void Finalizer() => Clock.Leave(91); }
    static class S92 { public static void Prefix() => Clock.Enter(92); public static void Finalizer() => Clock.Leave(92); }
    static class S93 { public static void Prefix() => Clock.Enter(93); public static void Finalizer() => Clock.Leave(93); }
    static class S94 { public static void Prefix() => Clock.Enter(94); public static void Finalizer() => Clock.Leave(94); }
    static class S95 { public static void Prefix() => Clock.Enter(95); public static void Finalizer() => Clock.Leave(95); }
    static class S96 { public static void Prefix() => Clock.Enter(96); public static void Finalizer() => Clock.Leave(96); }
    static class S97 { public static void Prefix() => Clock.Enter(97); public static void Finalizer() => Clock.Leave(97); }
    static class S98 { public static void Prefix() => Clock.Enter(98); public static void Finalizer() => Clock.Leave(98); }
    static class S99 { public static void Prefix() => Clock.Enter(99); public static void Finalizer() => Clock.Leave(99); }
    static class S100 { public static void Prefix() => Clock.Enter(100); public static void Finalizer() => Clock.Leave(100); }
    static class S101 { public static void Prefix() => Clock.Enter(101); public static void Finalizer() => Clock.Leave(101); }
    static class S102 { public static void Prefix() => Clock.Enter(102); public static void Finalizer() => Clock.Leave(102); }
    static class S103 { public static void Prefix() => Clock.Enter(103); public static void Finalizer() => Clock.Leave(103); }
    static class S104 { public static void Prefix() => Clock.Enter(104); public static void Finalizer() => Clock.Leave(104); }
    static class S105 { public static void Prefix() => Clock.Enter(105); public static void Finalizer() => Clock.Leave(105); }
    static class S106 { public static void Prefix() => Clock.Enter(106); public static void Finalizer() => Clock.Leave(106); }
    static class S107 { public static void Prefix() => Clock.Enter(107); public static void Finalizer() => Clock.Leave(107); }
    static class S108 { public static void Prefix() => Clock.Enter(108); public static void Finalizer() => Clock.Leave(108); }
    static class S109 { public static void Prefix() => Clock.Enter(109); public static void Finalizer() => Clock.Leave(109); }
    static class S110 { public static void Prefix() => Clock.Enter(110); public static void Finalizer() => Clock.Leave(110); }
    static class S111 { public static void Prefix() => Clock.Enter(111); public static void Finalizer() => Clock.Leave(111); }
    static class S112 { public static void Prefix() => Clock.Enter(112); public static void Finalizer() => Clock.Leave(112); }
    static class S113 { public static void Prefix() => Clock.Enter(113); public static void Finalizer() => Clock.Leave(113); }
    static class S114 { public static void Prefix() => Clock.Enter(114); public static void Finalizer() => Clock.Leave(114); }
    static class S115 { public static void Prefix() => Clock.Enter(115); public static void Finalizer() => Clock.Leave(115); }
    static class S116 { public static void Prefix() => Clock.Enter(116); public static void Finalizer() => Clock.Leave(116); }
    static class S117 { public static void Prefix() => Clock.Enter(117); public static void Finalizer() => Clock.Leave(117); }
    static class S118 { public static void Prefix() => Clock.Enter(118); public static void Finalizer() => Clock.Leave(118); }
    static class S119 { public static void Prefix() => Clock.Enter(119); public static void Finalizer() => Clock.Leave(119); }
    static class S120 { public static void Prefix() => Clock.Enter(120); public static void Finalizer() => Clock.Leave(120); }
    static class S121 { public static void Prefix() => Clock.Enter(121); public static void Finalizer() => Clock.Leave(121); }
    static class S122 { public static void Prefix() => Clock.Enter(122); public static void Finalizer() => Clock.Leave(122); }
    static class S123 { public static void Prefix() => Clock.Enter(123); public static void Finalizer() => Clock.Leave(123); }
    static class S124 { public static void Prefix() => Clock.Enter(124); public static void Finalizer() => Clock.Leave(124); }
    static class S125 { public static void Prefix() => Clock.Enter(125); public static void Finalizer() => Clock.Leave(125); }
    static class S126 { public static void Prefix() => Clock.Enter(126); public static void Finalizer() => Clock.Leave(126); }
    static class S127 { public static void Prefix() => Clock.Enter(127); public static void Finalizer() => Clock.Leave(127); }
    static class S128 { public static void Prefix() => Clock.Enter(128); public static void Finalizer() => Clock.Leave(128); }
    static class S129 { public static void Prefix() => Clock.Enter(129); public static void Finalizer() => Clock.Leave(129); }
    static class S130 { public static void Prefix() => Clock.Enter(130); public static void Finalizer() => Clock.Leave(130); }
    static class S131 { public static void Prefix() => Clock.Enter(131); public static void Finalizer() => Clock.Leave(131); }
    static class S132 { public static void Prefix() => Clock.Enter(132); public static void Finalizer() => Clock.Leave(132); }
    static class S133 { public static void Prefix() => Clock.Enter(133); public static void Finalizer() => Clock.Leave(133); }
    static class S134 { public static void Prefix() => Clock.Enter(134); public static void Finalizer() => Clock.Leave(134); }
    static class S135 { public static void Prefix() => Clock.Enter(135); public static void Finalizer() => Clock.Leave(135); }
    static class S136 { public static void Prefix() => Clock.Enter(136); public static void Finalizer() => Clock.Leave(136); }
    static class S137 { public static void Prefix() => Clock.Enter(137); public static void Finalizer() => Clock.Leave(137); }
    static class S138 { public static void Prefix() => Clock.Enter(138); public static void Finalizer() => Clock.Leave(138); }
    static class S139 { public static void Prefix() => Clock.Enter(139); public static void Finalizer() => Clock.Leave(139); }
    static class S140 { public static void Prefix() => Clock.Enter(140); public static void Finalizer() => Clock.Leave(140); }
    static class S141 { public static void Prefix() => Clock.Enter(141); public static void Finalizer() => Clock.Leave(141); }
    static class S142 { public static void Prefix() => Clock.Enter(142); public static void Finalizer() => Clock.Leave(142); }
    static class S143 { public static void Prefix() => Clock.Enter(143); public static void Finalizer() => Clock.Leave(143); }
    static class S144 { public static void Prefix() => Clock.Enter(144); public static void Finalizer() => Clock.Leave(144); }
    static class S145 { public static void Prefix() => Clock.Enter(145); public static void Finalizer() => Clock.Leave(145); }
    static class S146 { public static void Prefix() => Clock.Enter(146); public static void Finalizer() => Clock.Leave(146); }
    static class S147 { public static void Prefix() => Clock.Enter(147); public static void Finalizer() => Clock.Leave(147); }
    static class S148 { public static void Prefix() => Clock.Enter(148); public static void Finalizer() => Clock.Leave(148); }
    static class S149 { public static void Prefix() => Clock.Enter(149); public static void Finalizer() => Clock.Leave(149); }
    static class S150 { public static void Prefix() => Clock.Enter(150); public static void Finalizer() => Clock.Leave(150); }
    static class S151 { public static void Prefix() => Clock.Enter(151); public static void Finalizer() => Clock.Leave(151); }
    static class S152 { public static void Prefix() => Clock.Enter(152); public static void Finalizer() => Clock.Leave(152); }
    static class S153 { public static void Prefix() => Clock.Enter(153); public static void Finalizer() => Clock.Leave(153); }
    static class S154 { public static void Prefix() => Clock.Enter(154); public static void Finalizer() => Clock.Leave(154); }
    static class S155 { public static void Prefix() => Clock.Enter(155); public static void Finalizer() => Clock.Leave(155); }
    static class S156 { public static void Prefix() => Clock.Enter(156); public static void Finalizer() => Clock.Leave(156); }
    static class S157 { public static void Prefix() => Clock.Enter(157); public static void Finalizer() => Clock.Leave(157); }
    static class S158 { public static void Prefix() => Clock.Enter(158); public static void Finalizer() => Clock.Leave(158); }
    static class S159 { public static void Prefix() => Clock.Enter(159); public static void Finalizer() => Clock.Leave(159); }
    static class S160 { public static void Prefix() => Clock.Enter(160); public static void Finalizer() => Clock.Leave(160); }
    static class S161 { public static void Prefix() => Clock.Enter(161); public static void Finalizer() => Clock.Leave(161); }
    static class S162 { public static void Prefix() => Clock.Enter(162); public static void Finalizer() => Clock.Leave(162); }
    static class S163 { public static void Prefix() => Clock.Enter(163); public static void Finalizer() => Clock.Leave(163); }
    static class S164 { public static void Prefix() => Clock.Enter(164); public static void Finalizer() => Clock.Leave(164); }
    static class S165 { public static void Prefix() => Clock.Enter(165); public static void Finalizer() => Clock.Leave(165); }
    static class S166 { public static void Prefix() => Clock.Enter(166); public static void Finalizer() => Clock.Leave(166); }
    static class S167 { public static void Prefix() => Clock.Enter(167); public static void Finalizer() => Clock.Leave(167); }
    static class S168 { public static void Prefix() => Clock.Enter(168); public static void Finalizer() => Clock.Leave(168); }
    static class S169 { public static void Prefix() => Clock.Enter(169); public static void Finalizer() => Clock.Leave(169); }
    static class S170 { public static void Prefix() => Clock.Enter(170); public static void Finalizer() => Clock.Leave(170); }
    static class S171 { public static void Prefix() => Clock.Enter(171); public static void Finalizer() => Clock.Leave(171); }
    static class S172 { public static void Prefix() => Clock.Enter(172); public static void Finalizer() => Clock.Leave(172); }
    static class S173 { public static void Prefix() => Clock.Enter(173); public static void Finalizer() => Clock.Leave(173); }
    static class S174 { public static void Prefix() => Clock.Enter(174); public static void Finalizer() => Clock.Leave(174); }
    static class S175 { public static void Prefix() => Clock.Enter(175); public static void Finalizer() => Clock.Leave(175); }
    static class S176 { public static void Prefix() => Clock.Enter(176); public static void Finalizer() => Clock.Leave(176); }
    static class S177 { public static void Prefix() => Clock.Enter(177); public static void Finalizer() => Clock.Leave(177); }
    static class S178 { public static void Prefix() => Clock.Enter(178); public static void Finalizer() => Clock.Leave(178); }
    static class S179 { public static void Prefix() => Clock.Enter(179); public static void Finalizer() => Clock.Leave(179); }
    static class S180 { public static void Prefix() => Clock.Enter(180); public static void Finalizer() => Clock.Leave(180); }
    static class S181 { public static void Prefix() => Clock.Enter(181); public static void Finalizer() => Clock.Leave(181); }
    static class S182 { public static void Prefix() => Clock.Enter(182); public static void Finalizer() => Clock.Leave(182); }
    static class S183 { public static void Prefix() => Clock.Enter(183); public static void Finalizer() => Clock.Leave(183); }
    static class S184 { public static void Prefix() => Clock.Enter(184); public static void Finalizer() => Clock.Leave(184); }
    static class S185 { public static void Prefix() => Clock.Enter(185); public static void Finalizer() => Clock.Leave(185); }
    static class S186 { public static void Prefix() => Clock.Enter(186); public static void Finalizer() => Clock.Leave(186); }
    static class S187 { public static void Prefix() => Clock.Enter(187); public static void Finalizer() => Clock.Leave(187); }
    static class S188 { public static void Prefix() => Clock.Enter(188); public static void Finalizer() => Clock.Leave(188); }
    static class S189 { public static void Prefix() => Clock.Enter(189); public static void Finalizer() => Clock.Leave(189); }
    static class S190 { public static void Prefix() => Clock.Enter(190); public static void Finalizer() => Clock.Leave(190); }
    static class S191 { public static void Prefix() => Clock.Enter(191); public static void Finalizer() => Clock.Leave(191); }
    static class S192 { public static void Prefix() => Clock.Enter(192); public static void Finalizer() => Clock.Leave(192); }
    static class S193 { public static void Prefix() => Clock.Enter(193); public static void Finalizer() => Clock.Leave(193); }
    static class S194 { public static void Prefix() => Clock.Enter(194); public static void Finalizer() => Clock.Leave(194); }
    static class S195 { public static void Prefix() => Clock.Enter(195); public static void Finalizer() => Clock.Leave(195); }
    static class S196 { public static void Prefix() => Clock.Enter(196); public static void Finalizer() => Clock.Leave(196); }
    static class S197 { public static void Prefix() => Clock.Enter(197); public static void Finalizer() => Clock.Leave(197); }
    static class S198 { public static void Prefix() => Clock.Enter(198); public static void Finalizer() => Clock.Leave(198); }
    static class S199 { public static void Prefix() => Clock.Enter(199); public static void Finalizer() => Clock.Leave(199); }
    static class S200 { public static void Prefix() => Clock.Enter(200); public static void Finalizer() => Clock.Leave(200); }
    static class S201 { public static void Prefix() => Clock.Enter(201); public static void Finalizer() => Clock.Leave(201); }
    static class S202 { public static void Prefix() => Clock.Enter(202); public static void Finalizer() => Clock.Leave(202); }
    static class S203 { public static void Prefix() => Clock.Enter(203); public static void Finalizer() => Clock.Leave(203); }
    static class S204 { public static void Prefix() => Clock.Enter(204); public static void Finalizer() => Clock.Leave(204); }
    static class S205 { public static void Prefix() => Clock.Enter(205); public static void Finalizer() => Clock.Leave(205); }
    static class S206 { public static void Prefix() => Clock.Enter(206); public static void Finalizer() => Clock.Leave(206); }
    static class S207 { public static void Prefix() => Clock.Enter(207); public static void Finalizer() => Clock.Leave(207); }
    static class S208 { public static void Prefix() => Clock.Enter(208); public static void Finalizer() => Clock.Leave(208); }
    static class S209 { public static void Prefix() => Clock.Enter(209); public static void Finalizer() => Clock.Leave(209); }
    static class S210 { public static void Prefix() => Clock.Enter(210); public static void Finalizer() => Clock.Leave(210); }
    static class S211 { public static void Prefix() => Clock.Enter(211); public static void Finalizer() => Clock.Leave(211); }
    static class S212 { public static void Prefix() => Clock.Enter(212); public static void Finalizer() => Clock.Leave(212); }
    static class S213 { public static void Prefix() => Clock.Enter(213); public static void Finalizer() => Clock.Leave(213); }
    static class S214 { public static void Prefix() => Clock.Enter(214); public static void Finalizer() => Clock.Leave(214); }
    static class S215 { public static void Prefix() => Clock.Enter(215); public static void Finalizer() => Clock.Leave(215); }
    static class S216 { public static void Prefix() => Clock.Enter(216); public static void Finalizer() => Clock.Leave(216); }
    static class S217 { public static void Prefix() => Clock.Enter(217); public static void Finalizer() => Clock.Leave(217); }
    static class S218 { public static void Prefix() => Clock.Enter(218); public static void Finalizer() => Clock.Leave(218); }
    static class S219 { public static void Prefix() => Clock.Enter(219); public static void Finalizer() => Clock.Leave(219); }
    static class S220 { public static void Prefix() => Clock.Enter(220); public static void Finalizer() => Clock.Leave(220); }
    static class S221 { public static void Prefix() => Clock.Enter(221); public static void Finalizer() => Clock.Leave(221); }
    static class S222 { public static void Prefix() => Clock.Enter(222); public static void Finalizer() => Clock.Leave(222); }
    static class S223 { public static void Prefix() => Clock.Enter(223); public static void Finalizer() => Clock.Leave(223); }
    static class S224 { public static void Prefix() => Clock.Enter(224); public static void Finalizer() => Clock.Leave(224); }
    static class S225 { public static void Prefix() => Clock.Enter(225); public static void Finalizer() => Clock.Leave(225); }
    static class S226 { public static void Prefix() => Clock.Enter(226); public static void Finalizer() => Clock.Leave(226); }
    static class S227 { public static void Prefix() => Clock.Enter(227); public static void Finalizer() => Clock.Leave(227); }
    static class S228 { public static void Prefix() => Clock.Enter(228); public static void Finalizer() => Clock.Leave(228); }
    static class S229 { public static void Prefix() => Clock.Enter(229); public static void Finalizer() => Clock.Leave(229); }
    static class S230 { public static void Prefix() => Clock.Enter(230); public static void Finalizer() => Clock.Leave(230); }
    static class S231 { public static void Prefix() => Clock.Enter(231); public static void Finalizer() => Clock.Leave(231); }
    static class S232 { public static void Prefix() => Clock.Enter(232); public static void Finalizer() => Clock.Leave(232); }
    static class S233 { public static void Prefix() => Clock.Enter(233); public static void Finalizer() => Clock.Leave(233); }
    static class S234 { public static void Prefix() => Clock.Enter(234); public static void Finalizer() => Clock.Leave(234); }
    static class S235 { public static void Prefix() => Clock.Enter(235); public static void Finalizer() => Clock.Leave(235); }
    static class S236 { public static void Prefix() => Clock.Enter(236); public static void Finalizer() => Clock.Leave(236); }
    static class S237 { public static void Prefix() => Clock.Enter(237); public static void Finalizer() => Clock.Leave(237); }
    static class S238 { public static void Prefix() => Clock.Enter(238); public static void Finalizer() => Clock.Leave(238); }
    static class S239 { public static void Prefix() => Clock.Enter(239); public static void Finalizer() => Clock.Leave(239); }
    static class S240 { public static void Prefix() => Clock.Enter(240); public static void Finalizer() => Clock.Leave(240); }
    static class S241 { public static void Prefix() => Clock.Enter(241); public static void Finalizer() => Clock.Leave(241); }
    static class S242 { public static void Prefix() => Clock.Enter(242); public static void Finalizer() => Clock.Leave(242); }
    static class S243 { public static void Prefix() => Clock.Enter(243); public static void Finalizer() => Clock.Leave(243); }
    static class S244 { public static void Prefix() => Clock.Enter(244); public static void Finalizer() => Clock.Leave(244); }
    static class S245 { public static void Prefix() => Clock.Enter(245); public static void Finalizer() => Clock.Leave(245); }
    static class S246 { public static void Prefix() => Clock.Enter(246); public static void Finalizer() => Clock.Leave(246); }
    static class S247 { public static void Prefix() => Clock.Enter(247); public static void Finalizer() => Clock.Leave(247); }
    static class S248 { public static void Prefix() => Clock.Enter(248); public static void Finalizer() => Clock.Leave(248); }
    static class S249 { public static void Prefix() => Clock.Enter(249); public static void Finalizer() => Clock.Leave(249); }
    static class S250 { public static void Prefix() => Clock.Enter(250); public static void Finalizer() => Clock.Leave(250); }
    static class S251 { public static void Prefix() => Clock.Enter(251); public static void Finalizer() => Clock.Leave(251); }
    static class S252 { public static void Prefix() => Clock.Enter(252); public static void Finalizer() => Clock.Leave(252); }
    static class S253 { public static void Prefix() => Clock.Enter(253); public static void Finalizer() => Clock.Leave(253); }
    static class S254 { public static void Prefix() => Clock.Enter(254); public static void Finalizer() => Clock.Leave(254); }
    static class S255 { public static void Prefix() => Clock.Enter(255); public static void Finalizer() => Clock.Leave(255); }
}
