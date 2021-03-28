﻿namespace hyperactive {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Input;

    using LibGit2Sharp;

    public class Repo: ViewModel {
        private string? directory = @"D:\DEV\git-conflicts"; // TODO: remove test value
        public string? Directory {
            get => directory;
            set {
                SetProperty(ref directory, value);
                LoadRepository();
            }
        }

        private Repository? repo;

        private RepoStatus status = new();
        public RepoStatus Status { get => status; private set => SetProperty(ref status, value); }

        private IBranch[]? branches;
        public IBranch[]? Branches { get => branches; private set => SetProperty(ref branches, value); }

        private int? localBranchesCount;
        public int? LocalBranchesCount { get => localBranchesCount; private set => SetProperty(ref localBranchesCount, value); }

        private int? remoteBranchesCount;
        public int? RemoteBranchesCount { get => remoteBranchesCount; private set => SetProperty(ref remoteBranchesCount, value); }

        private bool isLoading;
        public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }

        private bool isLoaded;
        public bool IsLoaded { get => isLoaded; private set => SetProperty(ref isLoaded, value); }

        public Repo() {
            SelectDirectoryCmd = new Command(SelectDirectory);
            LoadRepositoryCmd = new Command(LoadRepository);
            WeakEventManager<Events, EventArgs>.AddHandler(Events.Instance, nameof(Events.WorkingTreeChanged), RefreshStatus);
            LoadRepository(); // TODO: remove test code
        }

        public ICommand SelectDirectoryCmd { get; }

        public ICommand LoadRepositoryCmd { get; }

        private void SelectDirectory() {
            using var dialog = new FolderBrowserDialog {
                Description = "Select Git Repository",
                UseDescriptionForTitle = true,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + Path.DirectorySeparatorChar,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == DialogResult.OK) {
                Directory = dialog.SelectedPath;
            }
        }

        private void LoadRepository() {
            IsLoading = true;

            Cleanup();

            repo = new Repository(Directory);

            Status = new(repo);

            Branches = repo
                .Branches
                .Where(b => !b.IsRemote)
                .Select(b => b.IsCurrentRepositoryHead
                    ? new WTreeBranch(Directory!, b)
                    : (IBranch)new ObjDbBranch(b))
                .OrderBy(b => b.Name, Comparer<string>.Create(DevelopFirstMainLast))
                .ToArray();

            LocalBranchesCount = Branches.Length;
            RemoteBranchesCount = repo.Branches.Count(b => b.IsRemote);

            IsLoading = false;
            IsLoaded = true;
        }

        private void RefreshStatus(object? sender, EventArgs args) => Status = new(repo!);

        private int DevelopFirstMainLast(string branch1, string branch2) => (branch1, branch2) switch {
            ("main"   , _        ) =>  1,
            (_        , "main"   ) => -1,
            ("master" , _        ) =>  1,
            (_        , "master" ) => -1,

            ("develop", _        ) => -1,
            (_        , "develop") =>  1,

            (_        , _        ) => branch1.CompareTo(branch2)
        };

        private void Cleanup() {
            Status = new();
            Branches = Array.Empty<IBranch>();
            LocalBranchesCount = null;
            RemoteBranchesCount = null;
            repo?.Dispose();
        }
    }
}