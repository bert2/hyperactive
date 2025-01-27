﻿namespace sabotage {
    using System;

    using LibGit2Sharp;

    public static class RepositoryExt {
        public static Signature CreateSignature(this Repository repo)
            => repo.Config.BuildSignature(DateTime.Now)
            ?? new Signature(new Identity("sabotage", "boom@sabotage"), DateTime.Now);
    }
}
