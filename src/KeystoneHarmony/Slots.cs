namespace KeystoneHarmony
{
    // Made by a script: one small class for each slot, each mod having a slot of its own, so that the timing that is
    // put round every measured method knows whose it is without looking anything up (see Clock).
    static class Slots
    {
        public const int Count = 64;
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
}
