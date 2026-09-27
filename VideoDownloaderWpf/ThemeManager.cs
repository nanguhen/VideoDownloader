// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Media;

namespace VideoDownloader
{
    internal static class ThemeManager
    {
        public static void ApplyTheme(int mode)
        {
            var p = ThemePalette.Get(mode);
            var resources = Application.Current.Resources;

            resources["FormBrush"] = new SolidColorBrush(p.Form);
            resources["PanelBrush"] = new SolidColorBrush(p.Panel);
            resources["TextBrush"] = new SolidColorBrush(p.Text);
            resources["MutedBrush"] = new SolidColorBrush(p.Muted);
            resources["InputBrush"] = new SolidColorBrush(p.Input);
            resources["BorderBrush"] = new SolidColorBrush(p.Border);
            resources["AccentBrush"] = new SolidColorBrush(p.Accent);
            resources["SecondaryBrush"] = new SolidColorBrush(p.Secondary);
            resources["ButtonBrush"] = new SolidColorBrush(p.Button);
            resources["ButtonTextBrush"] = new SolidColorBrush(p.ButtonText);
            resources["HoverBrush"] = new SolidColorBrush(p.Hover);
            resources["HeadingBrush"] = new SolidColorBrush(p.Heading);
            resources["LogBackBrush"] = new SolidColorBrush(p.LogBack);
            resources["LogForeBrush"] = new SolidColorBrush(p.LogFore);
        }
    }

    internal sealed class ThemePalette
    {
        public Color Form { get; private set; }
        public Color Panel { get; private set; }
        public Color Text { get; private set; }
        public Color Muted { get; private set; }
        public Color Input { get; private set; }
        public Color Border { get; private set; }
        public Color Accent { get; private set; }
        public Color Secondary { get; private set; }
        public Color Button { get; private set; }
        public Color ButtonText { get; private set; }
        public Color Hover { get; private set; }
        public Color Heading { get; private set; }
        public Color LogBack { get; private set; }
        public Color LogFore { get; private set; }

        private ThemePalette(
            string form,
            string panel,
            string text,
            string muted,
            string input,
            string border,
            string accent,
            string secondary,
            string button,
            string buttonText,
            string hover,
            string heading,
            string logBack,
            string logFore)
        {
            Form = Hex(form);
            Panel = Hex(panel);
            Text = Hex(text);
            Muted = Hex(muted);
            Input = Hex(input);
            Border = Hex(border);
            Accent = Hex(accent);
            Secondary = Hex(secondary);
            Button = Hex(button);
            ButtonText = Hex(buttonText);
            Hover = Hex(hover);
            Heading = Hex(heading);
            LogBack = Hex(logBack);
            LogFore = Hex(logFore);
        }

        private static Color Hex(string value)
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }

        public static ThemePalette Get(int mode)
        {
            if (mode == 1)
            {
                return new ThemePalette(
                    "#EFF3F8",
                    "#FFFFFF",
                    "#1B2A3A",
                    "#5A6B7F",
                    "#FFFFFF",
                    "#D4DDE7",
                    "#3B82C4",
                    "#F59E0B",
                    "#E8EEF5",
                    "#1B2A3A",
                    "#DCE5EF",
                    "#10212F",
                    "#EAF0F5",
                    "#1F2A36");
            }

            if (mode == 2)
            {
                return new ThemePalette(
                    "#F1F8F7",
                    "#FFFFFF",
                    "#16312F",
                    "#4E6A67",
                    "#FFFFFF",
                    "#CDE2DE",
                    "#0F766E",
                    "#EA8C22",
                    "#E5F2EF",
                    "#16312F",
                    "#D5E9E5",
                    "#0B2926",
                    "#E8F2F0",
                    "#16312F");
            }

            return new ThemePalette(
                "#F7F9FC",
                "#FFFFFF",
                "#172033",
                "#5B6577",
                "#FFFFFF",
                "#D9E0E8",
                "#0D9488",
                "#F59E0B",
                "#EEF3F7",
                "#172033",
                "#E1E8EF",
                "#101828",
                "#F1F5F9",
                "#1F2937");
        }
    }
}
