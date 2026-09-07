using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
 
namespace Member1_DataGeneration
{
    /// <summary>
    /// Member 4's deliverable: the user-facing window. Wires up Member 1's
    /// data generation, Member 2's queue simulation, and Member 3's performance
    /// analysis behind an input form, a results grid, and a summary panel —
    /// no console prompts, no CSV round-trips needed since everything runs
    /// in-process.
    /// </summary>
    public class MainForm : Form
    {
        private NumericUpDown customerCountInput = null!;
        private TextBox seedInput = null!;
        private Button runButton = null!;
        private DataGridView resultsGrid = null!;
        private TextBox summaryBox = null!;
        private Label statusLabel = null!;
 
        private HistoricalDataAnalyzer? analyzer;
 
        public MainForm()
        {
            Text = "Single Server Queue Simulation";
            Width = 1150;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
 
            BuildLayout();
            LoadHistoricalData();
        }
 
        private void BuildLayout()
        {
            var inputPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 55,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.LeftToRight
            };
 
            inputPanel.Controls.Add(new Label
            {
                Text = "Customers to simulate:",
                AutoSize = true,
                Margin = new Padding(0, 10, 5, 0)
            });
            customerCountInput = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 50, Width = 80 };
            inputPanel.Controls.Add(customerCountInput);
 
            inputPanel.Controls.Add(new Label
            {
                Text = "Random seed (optional):",
                AutoSize = true,
                Margin = new Padding(25, 10, 5, 0)
            });
            seedInput = new TextBox { Width = 80 };
            inputPanel.Controls.Add(seedInput);
 
            runButton = new Button { Text = "Run Simulation", Width = 140, Height = 30, Margin = new Padding(25, 3, 0, 0) };
            runButton.Click += RunButton_Click;
            inputPanel.Controls.Add(runButton);
 
            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.WhiteSmoke
            };
 
            resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White
            };
 
            summaryBox = new TextBox
            {
                Dock = DockStyle.Right,
                Width = 330,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font(FontFamily.GenericMonospace, 9.5f),
                Text = "Run a simulation to see the performance summary here."
            };
 
            // Order matters for Dock: Fill last among these three, but since Fill
            // is added first here it still works because .NET docks in reverse
            // z-order — Right/Bottom/Top get their edge first, Fill takes what's left.
            Controls.Add(resultsGrid);
            Controls.Add(summaryBox);
            Controls.Add(inputPanel);
            Controls.Add(statusLabel);
        }
 
        private void LoadHistoricalData()
        {
            try
            {
                string csvPath = Path.Combine(AppContext.BaseDirectory, "SapphireData.csv");
                if (!File.Exists(csvPath))
                    csvPath = "SapphireData.csv";
 
                analyzer = new HistoricalDataAnalyzer();
                analyzer.LoadAndFit(csvPath);
 
                statusLabel.Text = $"Historical data loaded — mean inter-arrival {analyzer.MeanInterArrivalMinutes:F2} min, " +
                                    $"mean service {analyzer.MeanServiceMinutes:F2} min. Ready to run.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Failed to load SapphireData.csv — see error.";
                MessageBox.Show($"Could not load historical data:\n{ex.Message}", "Startup Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
 
        private void RunButton_Click(object? sender, EventArgs e)
        {
            if (analyzer == null)
            {
                MessageBox.Show("Historical data isn't loaded — can't run a simulation.", "Not Ready",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
 
            int customerCount = (int)customerCountInput.Value;
 
            int? seed = null;
            if (!string.IsNullOrWhiteSpace(seedInput.Text))
            {
                if (!int.TryParse(seedInput.Text, out int parsedSeed))
                {
                    MessageBox.Show("Seed must be a whole number, or left blank.", "Invalid Seed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                seed = parsedSeed;
            }
 
            try
            {
                runButton.Enabled = false;
                statusLabel.Text = "Running simulation...";
 
                // Member 1: generate customers from the fitted historical distributions
                var generator = new DataGenerator(analyzer, seed: seed);
                var customers = generator.Generate(customerCount);
 
                // Member 2: push them through the single-server FCFS queue
                var simulation = new QueueServerSimulation();
                var results = simulation.Run(customers);
 
                // Member 3: compute the performance measures
                var perfAnalyzer = new PerformanceAnalyzer();
                var metrics = perfAnalyzer.Analyze(results);
 
                resultsGrid.DataSource = null;
                resultsGrid.Columns.Clear();
                resultsGrid.DataSource = results;
 
                summaryBox.Text = BuildSummaryText(generator, metrics);
 
                statusLabel.Text = $"Done — {results.Count} customers simulated using {generator.ServiceDistribution} service times.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Simulation failed — see error.";
                MessageBox.Show($"Something went wrong while running the simulation:\n{ex.Message}",
                    "Simulation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                runButton.Enabled = true;
            }
        }
 
        private static string BuildSummaryText(DataGenerator generator, PerformanceMetrics m)
        {
            return
                "PERFORMANCE SUMMARY\r\n" +
                "====================\r\n" +
                $"Service distribution : {generator.ServiceDistribution}\r\n\r\n" +
                $"Total customers      : {m.TotalCustomers}\r\n" +
                $"Total sim time (min) : {m.TotalSimulationTime:F2}\r\n\r\n" +
                $"Avg waiting time     : {m.AverageWaitingTime:F2}\r\n" +
                $"Max waiting time     : {m.MaxWaitingTime:F2}\r\n\r\n" +
                $"Avg time in system   : {m.AverageTimeInSystem:F2}\r\n" +
                $"Max time in system   : {m.MaxTimeInSystem:F2}\r\n\r\n" +
                $"Avg queue length     : {m.AverageQueueLength:F2}\r\n" +
                $"Max queue length     : {m.MaxQueueLength}\r\n\r\n" +
                $"Busy time (min)      : {m.TotalBusyTime:F2}\r\n" +
                $"Idle time (min)      : {m.TotalIdleTime:F2}\r\n" +
                $"Server utilization   : {m.ServerUtilization:P2}\r\n\r\n" +
                $"Customers who waited : {m.NumberWhoWaited} / {m.TotalCustomers}\r\n" +
                $"P(waiting)           : {m.ProbabilityOfWaiting:P2}\r\n";
        }
    }
}