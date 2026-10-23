using System.Drawing;
using BrightScroll.Core;

namespace BrightScroll.Windows;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly string _settingsPath;
    private readonly Action _settingsChanged;
    private readonly FlowLayoutPanel _displayList;
    private readonly Label _errorLabel;
    private readonly FontDialog _fontDialog;

    internal SettingsForm(AppSettings settings, string settingsPath, Action settingsChanged, Action exit)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _settingsChanged = settingsChanged;
        Text = "BrightScroll settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(560, 420);

        _fontDialog = new FontDialog { Font = CreateDialogFont("Segoe UI"), ShowColor = false };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(root);

        var settingsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        settingsPanel.Controls.Add(new Label { Text = "Scroll increment (%)", AutoSize = true });
        var increment = new NumericUpDown { Minimum = 1, Maximum = 100, Value = settings.Increment, Width = 90 };
        increment.ValueChanged += (_, _) => ChangeSettings(() => _settings.Increment = (int)increment.Value);
        settingsPanel.Controls.Add(increment);

        root.Controls.Add(settingsPanel, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "Connected displays",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true
        }, 0, 1);
        _displayList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        root.Controls.Add(_displayList, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _errorLabel = new Label { ForeColor = Color.Firebrick, AutoSize = true, Anchor = AnchorStyles.Left };
        var exitButton = new Button { Text = "Exit BrightScroll", AutoSize = true, Anchor = AnchorStyles.Right };
        exitButton.Click += (_, _) => exit();
        footer.Controls.Add(_errorLabel, 0, 0);
        footer.Controls.Add(exitButton, 1, 0);
        root.Controls.Add(footer, 0, 3);

        FormClosing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
        FormClosed += (_, _) => _fontDialog.Dispose();
    }

    internal void UpdateDisplays(IEnumerable<DisplayEntry> displays)
    {
        _displayList.SuspendLayout();
        foreach (Control control in _displayList.Controls)
        {
            control.Dispose();
        }

        _displayList.Controls.Clear();
        foreach (var display in displays)
        {
            var appearanceIdentity = display.AppearanceIdentity;
            var appearance = _settings.GetAppearance(appearanceIdentity);
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 2, 0, 4)
            };
            var text = display.Status?.IsAvailable == true
                ? $"{display.DisplayName}: {display.Status.Percent}%"
                : $"{display.DisplayName}: {display.Status?.Message ?? "Reading brightness…"}";
            row.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(280, 0),
                Margin = new Padding(3, 8, 8, 3)
            });

            var fontButton = new Button { Text = "Font…", AutoSize = true };
            fontButton.Click += (_, _) =>
            {
                _fontDialog.Font = CreateDialogFont(_settings.GetAppearance(appearanceIdentity).FontFamily);
                if (_fontDialog.ShowDialog(this) == DialogResult.OK)
                {
                    ChangeSettings(() =>
                        _settings.GetAppearance(appearanceIdentity).FontFamily = _fontDialog.Font.Name);
                }
            };
            var textColorButton = new Button { Text = "Text color…", AutoSize = true };
            textColorButton.Click += (_, _) =>
            {
                var currentColor = _settings.GetAppearance(appearanceIdentity).TextColor;
                ChooseColor(
                    currentColor,
                    value => _settings.GetAppearance(appearanceIdentity).TextColor = value);
            };
            row.Controls.Add(fontButton);
            row.Controls.Add(textColorButton);
            _displayList.Controls.Add(row);
        }

        _displayList.ResumeLayout();
    }

    internal void ShowError(string message) => _errorLabel.Text = message;

    private void ChooseColor(string current, Action<string> update)
    {
        using var dialog = new ColorDialog { Color = ColorTranslator.FromHtml(current), FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ChangeSettings(() => update($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"));
        }
    }

    private void ChangeSettings(Action change)
    {
        var previous = _settings.CreateSnapshot();
        change();
        try
        {
            _settings.Save(_settingsPath);
            _errorLabel.Text = string.Empty;
            _settingsChanged();
        }
        catch (Exception exception)
        {
            _settings.Restore(previous);
            _errorLabel.Text = $"Could not save settings: {exception.Message}";
        }
    }

    private static Font CreateDialogFont(string family)
    {
        try
        {
            return new Font(family, 12);
        }
        catch (ArgumentException)
        {
            return new Font("Segoe UI", 12);
        }
    }
}

internal sealed class DisplayEntry(
    string deviceName,
    string displayName,
    string appearanceIdentity,
    BrightnessStatus? status)
{
    internal string DeviceName { get; } = deviceName;
    internal string DisplayName { get; set; } = displayName;
    internal string AppearanceIdentity { get; set; } = appearanceIdentity;
    internal Guid Id { get; set; } = NotificationIconIdentity.Create(appearanceIdentity);
    internal int Index { get; set; }
    internal Icon? Icon { get; set; }
    internal BrightnessStatus? Status { get; set; } = status;
}
