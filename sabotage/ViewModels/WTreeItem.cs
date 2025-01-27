﻿namespace sabotage {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using LibGit2Sharp;

    using MoreLinq;

    public class WTreeItem: ViewModel, IDirectoryItem {
        private readonly Repository repo;

        // true when item is the "[..]" entry that navigates backwards
        private readonly bool isVirtual;

        public LocalBranch Parent { get; }

        public string Name { get; }

        public string Path { get; }

        public ItemType Type { get; }

        // status is retrieved lazily when item is scrolled into view
        private ItemStatus? status;
        public ItemStatus Status => status ??=
            isVirtual ? ItemStatus.Unchanged
            : Type == ItemType.File ? GetFileStatus(Path)
            : GetFolderStatus(Path);

        // content is read lazily when item is selected
        private string? content;
        public string? Content {
            get => content ??= Type == ItemType.File ? File.ReadAllText(Path) : null;
            set {
                if (SetProp(ref content, value)) {
                    File.WriteAllText(Path, value);
                    ResetStatus();
                    Events.RaiseWTreeModified();
                }
            }
        }

        public bool ReadOnly { get; } = false;

        public WTreeItem(WTreeBranch parent, FileSystemInfo fsi) {
            repo = parent.Parent.LibGitRepo;
            isVirtual = false;
            Parent = parent;
            Name = fsi.Name;
            Path = fsi.FullName;
            Type = GetItemType(fsi);
        }

        // used to create the "[..]" entry that navigates backwards
        public WTreeItem(WTreeBranch parent, string name, string path) {
            repo = parent.Parent.LibGitRepo;
            isVirtual = true;
            Parent = parent;
            Name = name;
            Path = path;
            Type = ItemType.Folder;
        }

        // will cause a new status retrieval next time the item is rendered
        public void ResetStatus() => SetProp(ref status, null, nameof(Status));

        private static ItemType GetItemType(FileSystemInfo fsi)
            => (fsi.Attributes & FileAttributes.Directory) != 0
                ? ItemType.Folder
                : ItemType.File;

        private ItemStatus GetFileStatus(string path) => repo
            .RetrieveStatus(path) switch {
                FileStatus.NewInWorkdir        => ItemStatus.Added,
                FileStatus.NewInIndex          => ItemStatus.Added,

                FileStatus.ModifiedInWorkdir   => ItemStatus.Modified,
                FileStatus.ModifiedInIndex     => ItemStatus.Modified,
                FileStatus.RenamedInWorkdir    => ItemStatus.Modified,
                FileStatus.RenamedInIndex      => ItemStatus.Modified,
                FileStatus.TypeChangeInWorkdir => ItemStatus.Modified,
                FileStatus.TypeChangeInIndex   => ItemStatus.Modified,

                FileStatus.Conflicted          => ItemStatus.Conflicted,

                FileStatus.Ignored             => ItemStatus.Ignored,

                FileStatus.Unaltered           => ItemStatus.Unchanged,

                FileStatus.DeletedFromWorkdir  => ItemStatus.Unchanged,
                FileStatus.DeletedFromIndex    => ItemStatus.Unchanged,
                FileStatus.Nonexistent         => ItemStatus.Unchanged,
                FileStatus.Unreadable          => ItemStatus.Unchanged,
                _                              => ItemStatus.Unchanged,
        };

        private ItemStatus GetFolderStatus(string path) => repo
            .RetrieveStatus(new StatusOptions {
                PathSpec = new[] { GetRelativeGitPath(path) },
                IncludeUntracked = true,
                DetectRenamesInWorkDir = false,
                DetectRenamesInIndex = false,
                RecurseUntrackedDirs = true
            })
            .Select(st => st.State switch {
                FileStatus.NewInWorkdir        => ItemStatus.Modified,
                FileStatus.NewInIndex          => ItemStatus.Modified,
                FileStatus.ModifiedInWorkdir   => ItemStatus.Modified,
                FileStatus.ModifiedInIndex     => ItemStatus.Modified,
                FileStatus.RenamedInWorkdir    => ItemStatus.Modified,
                FileStatus.RenamedInIndex      => ItemStatus.Modified,
                FileStatus.TypeChangeInWorkdir => ItemStatus.Modified,
                FileStatus.TypeChangeInIndex   => ItemStatus.Modified,
                FileStatus.DeletedFromWorkdir  => ItemStatus.Modified,
                FileStatus.DeletedFromIndex    => ItemStatus.Modified,

                FileStatus.Conflicted          => ItemStatus.Conflicted,

                FileStatus.Unaltered           => ItemStatus.Unchanged,
                FileStatus.Ignored             => ItemStatus.Unchanged,
                FileStatus.Nonexistent         => ItemStatus.Unchanged,
                FileStatus.Unreadable          => ItemStatus.Unchanged,
                _                              => ItemStatus.Unchanged,
            })
            .MaxBy(st => st, Comparer<ItemStatus>.Create(ConflictedFirstUnchangedLast))
            .DefaultIfEmpty(ItemStatus.Unchanged)
            .First();

        private static int ConflictedFirstUnchangedLast(ItemStatus a, ItemStatus b) => (a, b) switch {
            (ItemStatus.Conflicted, _                    ) =>  1,
            (_                    , ItemStatus.Conflicted) => -1,

            (ItemStatus.Unchanged , _                    ) => -1,
            (_                    , ItemStatus.Unchanged ) =>  1,

            (ItemStatus.Modified  , ItemStatus.Modified  ) =>  0,

            _ => throw new NotSupportedException($"Unexpected folder item status when comparing {a} vs {b}.")
        };

        private string GetRelativeGitPath(string path) => System.IO.Path
            .GetRelativePath(Parent.Parent.Path, path)
            .Replace('\\', '/');
    }
}
