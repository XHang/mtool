using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AutoInjectPlugin
{
    public partial class DuplicateGameView : PluginUserControl
    {
        private readonly IPlayniteAPI api;
        private readonly List<CheckBox> gameCheckBoxes = new List<CheckBox>();

        public DuplicateGameView(IPlayniteAPI api)
        {
            InitializeComponent();
            this.api = api;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ScanDuplicates();
        }

        private void ScanDuplicates()
        {
            ResultsPanel.Children.Clear();
            gameCheckBoxes.Clear();

            List<Game> games;
            try
            {
                games = api.Database.Games.ToList();
            }
            catch (Exception ex)
            {
                api.Dialogs.ShowErrorMessage("读取 Playnite 游戏库失败：" + ex.Message, "检查重复游戏失败");
                return;
            }

            if (games.Count == 0)
            {
                ResultsPanel.Children.Add(new TextBlock { Text = "游戏库中没有游戏。", Margin = new Thickness(0, 8, 0, 0) });
                return;
            }

            int[] parent = Enumerable.Range(0, games.Count).ToArray();

            Func<int, int> find = null;
            find = index =>
            {
                if (parent[index] != index)
                    parent[index] = find(parent[index]);
                return parent[index];
            };

            Action<int, int> union = (left, right) =>
            {
                int leftRoot = find(left);
                int rightRoot = find(right);
                if (leftRoot != rightRoot)
                    parent[rightRoot] = leftRoot;
            };

            for (int i = 0; i < games.Count; i++)
            {
                for (int j = i + 1; j < games.Count; j++)
                {
                    if (IsDuplicate(games[i], games[j]))
                        union(i, j);
                }
            }

            var duplicateGroups = new Dictionary<int, List<int>>();
            for (int i = 0; i < games.Count; i++)
            {
                int root = find(i);
                if (!duplicateGroups.ContainsKey(root))
                    duplicateGroups[root] = new List<int>();
                duplicateGroups[root].Add(i);
            }

            var duplicateOnly = duplicateGroups.Values.Where(group => group.Count > 1).ToList();
            if (duplicateOnly.Count == 0)
            {
                ResultsPanel.Children.Add(new TextBlock { Text = "未找到重复游戏。", Margin = new Thickness(0, 8, 0, 0) });
                return;
            }

            ResultsPanel.Children.Add(new TextBlock
            {
                Text = "找到 " + duplicateOnly.Count + " 组重复游戏，请勾选需要删除的游戏：",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 8)
            });

            foreach (var group in duplicateOnly)
            {
                var border = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(10)
                };

                var panel = new StackPanel();
                var reasons = GetDuplicateReasons(games, group);
                panel.Children.Add(new TextBlock
                {
                    Text = "重复原因：" + string.Join("；", reasons),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 6)
                });

                foreach (int gameIndex in group)
                {
                    Game game = games[gameIndex];
                    var checkBox = new CheckBox
                    {
                        Tag = game,
                        Margin = new Thickness(0, 3, 0, 3),
                        Content = new TextBlock
                        {
                            Text = game.Name + "\n安装目录：" + (game.InstallDirectory ?? "（未设置）")
                        }
                    };
                    gameCheckBoxes.Add(checkBox);
                    panel.Children.Add(checkBox);
                }

                border.Child = panel;
                ResultsPanel.Children.Add(border);
            }

            var deleteButton = new Button
            {
                Content = "删除所选游戏",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 5, 0, 0)
            };
            deleteButton.Click += DeleteSelected_Click;
            ResultsPanel.Children.Add(deleteButton);
        }

        private bool IsDuplicate(Game left, Game right)
        {
            if (NamesMatch(left.Name, right.Name))
                return true;

            if (InstallDirectoriesMatch(left.InstallDirectory, right.InstallDirectory))
                return true;

            return ArgumentsMatch(GetFirstArguments(left), GetFirstArguments(right));
        }

        private static bool NamesMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool InstallDirectoriesMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            string leftNormalized = left.Trim().TrimEnd('\\', '/');
            string rightNormalized = right.Trim().TrimEnd('\\', '/');
            if (leftNormalized.Length == 0 || rightNormalized.Length == 0)
                return false;

            return string.Equals(leftNormalized, rightNormalized, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ArgumentsMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
        }

        private static string GetFirstArguments(Game game)
        {
            var action = game.GameActions?.FirstOrDefault();
            return action == null ? null : action.Arguments;
        }

        private static List<string> GetDuplicateReasons(List<Game> games, List<int> group)
        {
            var reasons = new List<string>();

            for (int i = 0; i < group.Count; i++)
            {
                for (int j = i + 1; j < group.Count; j++)
                {
                    Game left = games[group[i]];
                    Game right = games[group[j]];

                    if (NamesMatch(left.Name, right.Name) && !reasons.Contains("游戏名相同"))
                        reasons.Add("游戏名相同");

                    if (InstallDirectoriesMatch(left.InstallDirectory, right.InstallDirectory) && !reasons.Contains("安装目录相同"))
                        reasons.Add("安装目录相同");

                    if (ArgumentsMatch(GetFirstArguments(left), GetFirstArguments(right)) && !reasons.Contains("第一个 Action 的 Arguments 相同"))
                        reasons.Add("第一个 Action 的 Arguments 相同");
                }
            }

            return reasons;
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = gameCheckBoxes
                .Where(checkBox => checkBox.IsChecked == true)
                .Select(checkBox => checkBox.Tag as Game)
                .Where(game => game != null)
                .Distinct()
                .ToList();

            if (selected.Count == 0)
            {
                api.Dialogs.ShowErrorMessage("请先勾选要删除的游戏。", "未选择游戏");
                return;
            }

            var result = api.Dialogs.ShowMessage(
                "确定从 Playnite 游戏库中删除选中的 " + selected.Count + " 个游戏吗？\n不会删除游戏安装文件。",
                "删除重复游戏",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;

            int deletedCount = 0;
            foreach (var game in selected)
            {
                try
                {
                    api.Database.Games.Remove(game);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    api.Dialogs.ShowErrorMessage("删除游戏失败：" + game.Name + "\n" + ex.Message, "删除失败");
                }
            }

            api.Dialogs.ShowMessage("已删除 " + deletedCount + " 个游戏。", "检查重复游戏");
            ScanDuplicates();
        }
    }
}
