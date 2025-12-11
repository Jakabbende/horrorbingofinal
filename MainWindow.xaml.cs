using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Hellbingojatel
{
    public partial class MainWindow : Window
    {
        // ================= DATA MODELS =================

        public class EnPlayer
        {
            public string Name { get; set; }
            public int[] Numbers { get; set; } = new int[15];
            public HashSet<int> MatchedNumbers { get; } = new HashSet<int>();
            public int MatchedCount => MatchedNumbers.Count;
            public void ResetForNight() => MatchedNumbers.Clear();
        }

        public class MyPlayer
        {
            public int[] Numbers { get; set; } = new int[15];
            public HashSet<int> MatchedNumbers { get; } = new HashSet<int>();
            public bool[] RowCompleted { get; set; } = new bool[3];
            public int MatchedCount => MatchedNumbers.Count;

            public void ResetForNight()
            {
                MatchedNumbers.Clear();
                RowCompleted = new bool[3];
            }
        }

        // ================= VARIABLES =================

        // Players and drawing
        private readonly List<EnPlayer> enemies = new List<EnPlayer>();
        private MyPlayer player = new MyPlayer();
        private readonly List<int> pulledNumbers = new List<int>();
        private readonly Random rng = new Random();

        // Game state
        private int moneyink = 15;
        private int night = 1;
        private bool matchOver = false;
        private bool campaignStarted = false; // Indicates if the campaign has started

        // Power-up stock
        private int stockSnipe = 0;
        private int stockSabotage = 0;
        private int stockShield = 0;
        private bool shieldActive = false; // Is the shield active tonight

        // Row indices (3 rows x 5 numbers)
        private readonly int[][] rowIndices = new int[][]
        {
            new [] { 0,1,2,3,4 },
            new [] { 5,6,7,8,9 },
            new [] { 10,11,12,13,14 }
        };

        // ================= CONSTRUCTOR =================

        public MainWindow()
        {
            InitializeComponent();

            // Initial state
            menu.Visibility = Visibility.Visible;
            game.Visibility = Visibility.Hidden;
            buyphase.Visibility = Visibility.Hidden;
            Won.Visibility = Visibility.Hidden;

            UpdateUI();
        }

        // ================= UI UPDATE =================

        private void UpdateUI()
        {
            // Game screen
            inkcount.Content = $"Inks: {moneyink}";
            playersAliveCount.Content = $"Players Alive: {enemies.Count + 1}";
            nightscount.Content = $"Night {night}";

            snipecount.Content = $"Snipe: {stockSnipe}";
            sabotagecount.Content = $"Sabotage: {stockSabotage}";
            shieldcount.Content = $"Shield: {stockShield}";

            shieldActiveLabel.Visibility = shieldActive ? Visibility.Visible : Visibility.Hidden;

            // Shop screen
            inkcount_buy.Content = $"Total Inks: {moneyink}";
            snipe_stock_buy.Content = $"Owned: {stockSnipe}";
            sabotage_stock_buy.Content = $"Owned: {stockSabotage}";
            shield_stock_buy.Content = $"Owned: {stockShield}";
        }

        private void RefreshPlayerCardUI()
        {
            // Updates the numbers on the card buttons
            for (int i = 0; i < 15; i++)
            {
                var btn = (Button)FindName($"m{i + 1}");
                if (btn != null)
                {
                    btn.Content = player.Numbers[i].ToString();
                    btn.Background = Brushes.White;
                    btn.IsEnabled = true;
                }
            }
        }

        // ================= MENU & GAME START =================

        // Main menu START button
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // On first start, go to shop to prepare
            menu.Visibility = Visibility.Hidden;
            buyphase.Visibility = Visibility.Visible;
            UpdateUI();
        }

        // START NEXT NIGHT button in Shop
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // If enemies haven't been generated yet (start of campaign), do it
            if (!campaignStarted)
            {
                GenerateInitialRoster();
                campaignStarted = true;
            }

            // Night preparation (reset)
            foreach (var epl in enemies) epl.ResetForNight();
            player.ResetForNight();

            pulledNumbers.Clear();
            pulledList.Text = "";
            matchOver = false;

            // Turn off shield (must be reactivated if wanted)
            shieldActive = false;

            // UI setup
            RefreshPlayerCardUI();
            buyphase.Visibility = Visibility.Hidden;
            game.Visibility = Visibility.Visible;
            statuscsheckeringame.Content = $"Night {night} started. Draw numbers!";

            UpdateUI();
        }

        private void GenerateInitialRoster()
        {
            enemies.Clear();
            // 49 enemies + player = 50 people
            for (int i = 0; i < 49; i++)
            {
                enemies.Add(new EnPlayer { Name = $"Prisoner#{i + 1}", Numbers = GenerateUniqueNumbers() });
            }
            player = new MyPlayer { Numbers = GenerateUniqueNumbers(), RowCompleted = new bool[3] };
        }

        private int[] GenerateUniqueNumbers()
        {
            HashSet<int> set = new HashSet<int>();
            while (set.Count < 15) set.Add(rng.Next(1, 91));
            return set.ToArray();
        }

        // ================= MAIN GAME LOGIC (DRAW) =================

        private void nextround_Click(object sender, RoutedEventArgs e)
        {
            if (matchOver) return;
            if (pulledNumbers.Count >= 90) return;

            // Draw number
            int current;
            do { current = rng.Next(1, 91); } while (pulledNumbers.Contains(current));

            ProcessNumber(current);
        }

        private void ProcessNumber(int number)
        {
            pulledNumbers.Add(number);
            pulledList.Text = string.Join(", ", pulledNumbers);
            statuscsheckeringame.Content = $"Pulled: {number}";

            // 1. Check enemies
            EnPlayer winnerEnemy = null;
            foreach (var epl in enemies)
            {
                if (epl.MatchedNumbers.Contains(number)) continue;

                if (epl.Numbers.Contains(number))
                {
                    epl.MatchedNumbers.Add(number);
                    if (epl.MatchedCount >= 15)
                    {
                        winnerEnemy = epl;
                        break;
                    }
                }
            }

            // 2. Check player
            bool playerWon = false;
            if (!player.MatchedNumbers.Contains(number))
            {
                if (player.Numbers.Contains(number))
                {
                    player.MatchedNumbers.Add(number);
                    MarkPlayerButton(number); // Turn button green

                    // Check row bonus
                    CheckRowBonus();

                    if (player.MatchedCount >= 15) playerWon = true;
                }
            }

            // 3. Does anyone have Bingo?
            if (winnerEnemy != null || playerWon)
            {
                EndNight(winnerEnemy, playerWon);
            }
        }

        private void MarkPlayerButton(int number)
        {
            for (int i = 0; i < 15; i++)
            {
                if (player.Numbers[i] == number)
                {
                    var btn = (Button)FindName($"m{i + 1}");
                    if (btn != null)
                    {
                        btn.Background = Brushes.LightGreen;
                        btn.Content = "X";
                    }
                }
            }
        }

        private void CheckRowBonus()
        {
            for (int ri = 0; ri < rowIndices.Length; ri++)
            {
                if (!player.RowCompleted[ri])
                {
                    bool allMatched = rowIndices[ri].All(idx => player.MatchedNumbers.Contains(player.Numbers[idx]));
                    if (allMatched)
                    {
                        player.RowCompleted[ri] = true;
                        moneyink += 10;
                        currentstatus.Content = $"Row completed! +10 Ink";
                        UpdateUI();
                    }
                }
            }
        }

        // ================= END OF NIGHT & RESULTS =================

        private void EndNight(EnPlayer enemyWinner, bool playerWon)
        {
            matchOver = true;

            // Find the worst player (for execution)
            var allStats = new List<(string name, int count, bool isPlayer)>();
            foreach (var e in enemies) allStats.Add((e.Name, e.MatchedCount, false));
            allStats.Add(("YOU", player.MatchedCount, true));

            // Sort in ascending order (fewest matches first)
            var sortedList = allStats.OrderBy(x => x.count).ToList();
            var loser = sortedList[0];

            // SHIELD MECHANIC: If you are the worst but have a shield
            if (loser.isPlayer && shieldActive)
            {
                MessageBox.Show("You had the fewest numbers, but your SHIELD saved you!");
                // The second worst is executed instead of you
                if (sortedList.Count > 1) loser = sortedList[1];
                else loser = sortedList[0];
            }

            // Switch screen to Win Screen
            game.Visibility = Visibility.Hidden;
            Won.Visibility = Visibility.Visible;

            // --- CASE 1: YOU WON (GAME OVER - SUCCESS) ---
            if (playerWon)
            {
                wonTitle.Content = "FREEDOM!";
                wonTitle.Foreground = Brushes.Gold;
                wonSubtitle.Content = "You collected all numbers and escaped!";
                pluszinkwonsc.Content = "Congratulations, you beat the game!";

                continueBtn.Content = "Main Menu";
                continueBtn.Tag = "Victory"; // This signals the button to reset
            }
            // --- CASE 2: YOU ARE ELIMINATED (GAME OVER - FAIL) ---
            else if (loser.isPlayer)
            {
                wonTitle.Content = "TERMINATED";
                wonTitle.Foreground = Brushes.Red;
                wonSubtitle.Content = "You had the fewest matches.";
                pluszinkwonsc.Content = "Game Over.";

                continueBtn.Content = "Back to Menu";
                continueBtn.Tag = "GameOver"; // This also resets
            }
            // --- CASE 3: YOU SURVIVED (CONTINUE TO SHOP) ---
            else
            {
                string winnerName = enemyWinner != null ? enemyWinner.Name : "Unknown";
                wonTitle.Content = $"{winnerName} IS FREE";
                wonTitle.Foreground = Brushes.White;
                wonSubtitle.Content = $"{winnerName} escaped. {loser.name} was executed.";

                moneyink += 10;
                pluszinkwonsc.Content = "+10 Ink for Surviving";

                continueBtn.Content = "Go to Shop";
                continueBtn.Tag = "NextNight"; // This leads to the shop

                // Remove enemies from list (only if game continues)
                if (enemyWinner != null) enemies.Remove(enemyWinner);

                var dead = enemies.FirstOrDefault(x => x.Name == loser.name);
                if (dead != null) enemies.Remove(dead);

                night++;
            }

            UpdateUI();
        }

        // ================= NAVIGATION (WON SCREEN BUTTON) =================

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            string tag = (string)continueBtn.Tag;

            // If Game Over OR Victory -> Back to main menu and full reset
            if (tag == "GameOver" || tag == "Victory")
            {
                // Reset everything
                campaignStarted = false;
                night = 1;
                moneyink = 15;

                stockSnipe = 0;
                stockSabotage = 0;
                stockShield = 0;
                shieldActive = false;

                enemies.Clear();
                pulledNumbers.Clear();

                // Switch screen
                menu.Visibility = Visibility.Visible;
                Won.Visibility = Visibility.Hidden;
                buyphase.Visibility = Visibility.Hidden;
                game.Visibility = Visibility.Hidden;

                UpdateUI();
            }
            // If NextNight -> Go to shop to prepare
            else
            {
                Won.Visibility = Visibility.Hidden;
                buyphase.Visibility = Visibility.Visible;
                UpdateUI();
            }
        }

        // ================= POWER-UPS =================

        // 1. SNIPE (Wildcard - Marks number only for player)
        private void UseSnipe_Click(object sender, RoutedEventArgs e)
        {
            if (stockSnipe > 0)
            {
                var unmatchedIndices = new List<int>();
                for (int i = 0; i < 15; i++)
                {
                    // We only look for numbers that aren't marked yet
                    if (!player.MatchedNumbers.Contains(player.Numbers[i]))
                        unmatchedIndices.Add(i);
                }

                if (unmatchedIndices.Count > 0)
                {
                    stockSnipe--;
                    UpdateUI();

                    // Randomly select one of the missing numbers
                    int rndIdx = unmatchedIndices[rng.Next(unmatchedIndices.Count)];
                    int numToMark = player.Numbers[rndIdx];

                    // --- FIXED: Add only to player, not global draw ---
                    player.MatchedNumbers.Add(numToMark);
                    MarkPlayerButton(numToMark); // Turn button green
                    CheckRowBonus();             // Row bonus

                    MessageBox.Show($"Snipe used! Number {numToMark} marked only for you.");

                    // Check if we won immediately with this
                    if (player.MatchedCount >= 15)
                    {
                        EndNight(null, true);
                    }
                }
                else
                {
                    MessageBox.Show("Card already full!");
                }
            }
            else MessageBox.Show("No Snipe available.");
        }

        // 2. SABOTAGE (Takes a point from the leader)
        private void UseSabotage_Click(object sender, RoutedEventArgs e)
        {
            if (stockSabotage > 0 && enemies.Count > 0)
            {
                stockSabotage--;
                // Find the best performing enemy
                var leader = enemies.OrderByDescending(x => x.MatchedCount).First();

                if (leader.MatchedCount > 0)
                {
                    int removeNum = leader.MatchedNumbers.First();
                    leader.MatchedNumbers.Remove(removeNum);
                    MessageBox.Show($"Sabotaged {leader.Name}! Removed match {removeNum}.");
                }
                else
                {
                    MessageBox.Show("Leader has no matches to remove.");
                }
                UpdateUI();
            }
            else MessageBox.Show("No Sabotage available.");
        }

        // 3. SHIELD (Protects from execution)
        private void UseShield_Click(object sender, RoutedEventArgs e)
        {
            if (stockShield > 0)
            {
                if (!shieldActive)
                {
                    stockShield--;
                    shieldActive = true;
                    UpdateUI();
                    MessageBox.Show("Shield Activated! You are safe from execution this night.");
                }
                else MessageBox.Show("Shield is already active.");
            }
            else MessageBox.Show("No Shield available.");
        }

        // ================= SHOP BUYING =================

        private void BuySnipe_Click(object sender, RoutedEventArgs e)
        {
            if (moneyink >= 15) { moneyink -= 15; stockSnipe++; UpdateUI(); }
        }
        private void BuySabotage_Click(object sender, RoutedEventArgs e)
        {
            if (moneyink >= 10) { moneyink -= 10; stockSabotage++; UpdateUI(); }
        }
        private void BuyShield_Click(object sender, RoutedEventArgs e)
        {
            if (moneyink >= 20) { moneyink -= 20; stockShield++; UpdateUI(); }
        }

        // ================= OTHER BUTTONS =================

        // Cheat button (In Main Menu)
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            moneyink = 9999;
            UpdateUI();
        }

        // Give Up button in game
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            game.Visibility = Visibility.Hidden;
            menu.Visibility = Visibility.Visible;
            campaignStarted = false;
        }
    }
}