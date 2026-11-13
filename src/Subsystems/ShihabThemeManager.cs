using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabCustomThemePreset
    {
        public string Name;
        public Color BackgroundMain;
        public Color BackgroundCard;
        public Color BackgroundSidebar;
        public Color AccentPrimary;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color GridAlternate;
    }

    public static class ShihabThemeManager
    {
        private const long MaxThemeTomlBytes = 256 * 1024;

        public static readonly string[] BuiltInThemeNames = new string[]
        {
            "Obsidian Dark",
            "Midnight Nord",
            "Emerald Matrix",
            "Solarized Dark",
            "Classic Light"
        };

        public static readonly string[] SupportedLanguages = new string[]
        {
            "en - English",
            "bn - বাংলা (Bengali)",
            "es - Español",
            "fr - Français",
            "de - Deutsch",
            "ru - Русский",
            "zh - 中文 (Simplified)",
            "ja - 日本語",
            "ar - العربية",
            "pt - Português",
            "hi - हिन्दी",
            "tr - Türkçe",
            "ko - 한국어",
            "it - Italiano"
        };

        private static readonly Dictionary<string, Dictionary<string, string>> UiTranslations = BuildTranslations();

        private static Dictionary<string, Dictionary<string, string>> BuildTranslations()
        {
            Dictionary<string, Dictionary<string, string>> map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> bn = new Dictionary<string, string>(StringComparer.Ordinal);
            bn["menu_tasks"] = "টাস্ক (&T)";
            bn["menu_file"] = "ফাইল (&F)";
            bn["menu_downloads"] = "ডাউনলোড (&D)";
            bn["menu_view"] = "ভিউ (&V)";
            bn["menu_tools"] = "টুলস (&L)";
            bn["menu_help"] = "সাহায্য (&H)";
            bn["menu_registration"] = "রেজিস্ট্রেশন (&R)";
            bn["btn_add_url"] = "URL যোগ করুন";
            bn["btn_resume"] = "চালু করুন";
            bn["btn_stop"] = "থামান";
            bn["btn_stop_all"] = "সব থামান";
            bn["btn_delete"] = "মুছুন";
            bn["btn_delete_completed"] = "সম্পন্ন মুছুন";
            bn["btn_options"] = "অপশন";
            bn["btn_scheduler"] = "শিডিউলার";
            bn["btn_start_queue"] = "কিউ শুরু";
            bn["btn_stop_queue"] = "কিউ বন্ধ";
            bn["btn_tell_friend"] = "বন্ধুকে জানান";
            bn["btn_grabber"] = "গ্র্যাবার";
            bn["check_updates"] = "আপডেট চেক করুন...";
            map["bn"] = bn;

            Dictionary<string, string> es = new Dictionary<string, string>(StringComparer.Ordinal);
            es["menu_tasks"] = "&Tareas";
            es["menu_file"] = "&Archivo";
            es["menu_downloads"] = "&Descargas";
            es["menu_view"] = "&Ver";
            es["menu_help"] = "A&yuda";
            es["menu_registration"] = "&Registro";
            es["btn_add_url"] = "Añadir URL";
            es["btn_resume"] = "Reanudar";
            es["btn_stop"] = "Detener";
            es["btn_stop_all"] = "Detener todo";
            es["btn_delete"] = "Eliminar";
            es["btn_delete_completed"] = "Borrar compl.";
            es["btn_options"] = "Opciones";
            es["btn_scheduler"] = "Programador";
            es["btn_start_queue"] = "Iniciar cola";
            es["btn_stop_queue"] = "Detener cola";
            es["btn_tell_friend"] = "Recomendar";
            es["check_updates"] = "Buscar actualizaciones...";
            map["es"] = es;

            Dictionary<string, string> fr = new Dictionary<string, string>(StringComparer.Ordinal);
            fr["menu_tasks"] = "&Tâches";
            fr["menu_file"] = "&Fichier";
            fr["menu_downloads"] = "&Téléchargements";
            fr["menu_view"] = "&Affichage";
            fr["menu_help"] = "Aid&e";
            fr["menu_registration"] = "En&registrement";
            fr["btn_add_url"] = "Ajouter URL";
            fr["btn_resume"] = "Reprendre";
            fr["btn_stop"] = "Arrêter";
            fr["btn_stop_all"] = "Tout arrêter";
            fr["btn_delete"] = "Supprimer";
            fr["btn_delete_completed"] = "Suppr. terminés";
            fr["btn_options"] = "Options";
            fr["btn_scheduler"] = "Planificateur";
            fr["btn_start_queue"] = "Démarrer file";
            fr["btn_stop_queue"] = "Arrêter file";
            fr["btn_tell_friend"] = "Partager";
            fr["check_updates"] = "Vérifier les mises à jour...";
            map["fr"] = fr;

            Dictionary<string, string> de = new Dictionary<string, string>(StringComparer.Ordinal);
            de["menu_tasks"] = "&Aufgaben";
            de["menu_file"] = "&Datei";
            de["menu_downloads"] = "&Downloads";
            de["menu_view"] = "&Ansicht";
            de["menu_help"] = "&Hilfe";
            de["menu_registration"] = "&Registrierung";
            de["btn_add_url"] = "URL hinzufügen";
            de["btn_resume"] = "Fortsetzen";
            de["btn_stop"] = "Stopp";
            de["btn_stop_all"] = "Alle stoppen";
            de["btn_delete"] = "Löschen";
            de["btn_delete_completed"] = "Fertige löschen";
            de["btn_options"] = "Optionen";
            de["btn_scheduler"] = "Zeitplaner";
            de["btn_start_queue"] = "Warteschl. Start";
            de["btn_stop_queue"] = "Warteschl. Stopp";
            de["btn_tell_friend"] = "Weiterempfehlen";
            de["check_updates"] = "Nach Updates suchen...";
            map["de"] = de;

            Dictionary<string, string> ar = new Dictionary<string, string>(StringComparer.Ordinal);
            ar["menu_tasks"] = "المهام (&T)";
            ar["menu_file"] = "ملف (&F)";
            ar["menu_downloads"] = "التحميلات (&D)";
            ar["menu_view"] = "عرض (&V)";
            ar["menu_help"] = "مساعدة (&H)";
            ar["menu_registration"] = "التسجيل (&R)";
            ar["btn_add_url"] = "إضافة رابط";
            ar["btn_resume"] = "استئناف";
            ar["btn_stop"] = "إيقاف";
            ar["btn_stop_all"] = "إيقاف الكل";
            ar["btn_delete"] = "حذف";
            ar["btn_delete_completed"] = "حذف المكتمل";
            ar["btn_options"] = "الخيارات";
            ar["btn_scheduler"] = "الجدولة";
            ar["btn_start_queue"] = "بدء القائمة";
            ar["btn_stop_queue"] = "إيقاف القائمة";
            ar["btn_tell_friend"] = "أخبر صديقاً";
            ar["check_updates"] = "التحقق من التحديثات...";
            map["ar"] = ar;

            Dictionary<string, string> ru = new Dictionary<string, string>(StringComparer.Ordinal);
            ru["menu_tasks"] = "&Задачи";
            ru["menu_file"] = "&Файл";
            ru["menu_downloads"] = "&Загрузки";
            ru["menu_view"] = "&Вид";
            ru["menu_help"] = "&Справка";
            ru["menu_registration"] = "&Регистрация";
            ru["btn_add_url"] = "Добавить URL";
            ru["btn_resume"] = "Продолжить";
            ru["btn_stop"] = "Остановить";
            ru["btn_stop_all"] = "Остановить все";
            ru["btn_delete"] = "Удалить";
            ru["btn_delete_completed"] = "Удалить заверш.";
            ru["btn_options"] = "Настройки";
            ru["btn_scheduler"] = "Планировщик";
            ru["btn_start_queue"] = "Запуск очереди";
            ru["btn_stop_queue"] = "Стоп очереди";
            ru["btn_tell_friend"] = "Рассказать другу";
            ru["check_updates"] = "Проверить обновления...";
            map["ru"] = ru;

            Dictionary<string, string> zh = new Dictionary<string, string>(StringComparer.Ordinal);
            zh["menu_tasks"] = "任务(&T)";
            zh["menu_file"] = "文件(&F)";
            zh["menu_downloads"] = "下载(&D)";
            zh["menu_view"] = "视图(&V)";
            zh["menu_help"] = "帮助(&H)";
            zh["menu_registration"] = "注册(&R)";
            zh["btn_add_url"] = "新建任务";
            zh["btn_resume"] = "继续";
            zh["btn_stop"] = "停止";
            zh["btn_stop_all"] = "全部停止";
            zh["btn_delete"] = "删除";
            zh["btn_delete_completed"] = "删除已完成";
            zh["btn_options"] = "选项";
            zh["btn_scheduler"] = "计划任务";
            zh["btn_start_queue"] = "开始队列";
            zh["btn_stop_queue"] = "停止队列";
            zh["btn_tell_friend"] = "推荐给好友";
            zh["check_updates"] = "检查更新...";
            map["zh"] = zh;

            Dictionary<string, string> ja = new Dictionary<string, string>(StringComparer.Ordinal);
            ja["menu_tasks"] = "タスク(&T)";
            ja["menu_file"] = "ファイル(&F)";
            ja["menu_downloads"] = "ダウンロード(&D)";
            ja["menu_view"] = "表示(&V)";
            ja["menu_help"] = "ヘルプ(&H)";
            ja["menu_registration"] = "登録情報(&R)";
            ja["btn_add_url"] = "URL追加";
            ja["btn_resume"] = "再開";
            ja["btn_stop"] = "停止";
            ja["btn_stop_all"] = "すべて停止";
            ja["btn_delete"] = "削除";
            ja["btn_delete_completed"] = "完了済みを削除";
            ja["btn_options"] = "設定";
            ja["btn_scheduler"] = "スケジューラ";
            ja["btn_start_queue"] = "キュー開始";
            ja["btn_stop_queue"] = "キュー停止";
            ja["btn_tell_friend"] = "友人に紹介";
            ja["check_updates"] = "更新を確認...";
            map["ja"] = ja;

            return map;
        }

        public static List<string> GetAllAvailableThemeNames(string baseDataDir)
        {
            List<string> names = new List<string>(BuiltInThemeNames);
            try
            {
                if (!string.IsNullOrWhiteSpace(baseDataDir))
                {
                    string themesDir = Path.Combine(baseDataDir, "themes");
                    if (Directory.Exists(themesDir))
                    {
                        string[] tomls = Directory.GetFiles(themesDir, "*.toml");
                        for (int i = 0; i < tomls.Length; i++)
                        {
                            string stem = Path.GetFileNameWithoutExtension(tomls[i]);
                            if (!string.IsNullOrWhiteSpace(stem) && !names.Contains(stem))
                            {
                                names.Add(stem);
                            }
                        }
                    }
                }
            }
            catch { }
            return names;
        }

        public static List<ShihabCustomThemePreset> GetBuiltInThemes()
        {
            List<ShihabCustomThemePreset> list = new List<ShihabCustomThemePreset>();
            for (int i = 0; i < BuiltInThemeNames.Length; i++)
            {
                list.Add(ResolveThemePalette(BuiltInThemeNames[i], ""));
            }
            return list;
        }

        public static bool IsDarkThemePreset(ShihabCustomThemePreset preset)
        {
            if (preset == null) return true;
            return ComputeRelativeLuminance(preset.BackgroundMain) < 0.5;
        }

        public static ShihabCustomThemePreset ResolveThemePalette(string themeName, string baseDataDir)
        {
            string t = (themeName ?? "Obsidian Dark").Trim();
            if (!string.IsNullOrWhiteSpace(baseDataDir))
            {
                ShihabCustomThemePreset custom = TryLoadCustomTomlTheme(t, baseDataDir);
                if (custom != null) return custom;
            }
            if (string.Equals(t, "Classic Light", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Classic Light",
                    BackgroundMain = Color.FromArgb(241, 245, 249),
                    BackgroundCard = Color.FromArgb(255, 255, 255),
                    BackgroundSidebar = Color.FromArgb(226, 232, 240),
                    AccentPrimary = Color.FromArgb(2, 132, 199),
                    TextPrimary = Color.FromArgb(15, 23, 42),
                    TextSecondary = Color.FromArgb(71, 85, 105),
                    GridAlternate = Color.FromArgb(248, 250, 252)
                };
            }
            if (string.Equals(t, "Midnight Nord", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Midnight Nord",
                    BackgroundMain = Color.FromArgb(46, 52, 64),
                    BackgroundCard = Color.FromArgb(59, 66, 82),
                    BackgroundSidebar = Color.FromArgb(36, 41, 51),
                    AccentPrimary = Color.FromArgb(136, 192, 208),
                    TextPrimary = Color.FromArgb(236, 239, 244),
                    TextSecondary = Color.FromArgb(216, 222, 233),
                    GridAlternate = Color.FromArgb(67, 76, 94)
                };
            }
            if (string.Equals(t, "Emerald Matrix", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Emerald Matrix",
                    BackgroundMain = Color.FromArgb(6, 20, 14),
                    BackgroundCard = Color.FromArgb(11, 34, 24),
                    BackgroundSidebar = Color.FromArgb(4, 15, 10),
                    AccentPrimary = Color.FromArgb(16, 185, 129),
                    TextPrimary = Color.FromArgb(209, 250, 229),
                    TextSecondary = Color.FromArgb(110, 231, 183),
                    GridAlternate = Color.FromArgb(14, 42, 30)
                };
            }
            if (string.Equals(t, "Solarized Dark", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Solarized Dark",
                    BackgroundMain = Color.FromArgb(0, 43, 54),
                    BackgroundCard = Color.FromArgb(7, 54, 66),
                    BackgroundSidebar = Color.FromArgb(0, 33, 43),
                    AccentPrimary = Color.FromArgb(38, 139, 210),
                    TextPrimary = Color.FromArgb(238, 232, 213),
                    TextSecondary = Color.FromArgb(147, 161, 161),
                    GridAlternate = Color.FromArgb(10, 62, 76)
                };
            }
            return new ShihabCustomThemePreset
            {
                Name = "Obsidian Dark",
                BackgroundMain = Color.FromArgb(14, 18, 28),
                BackgroundCard = Color.FromArgb(21, 27, 40),
                BackgroundSidebar = Color.FromArgb(17, 22, 33),
                AccentPrimary = Color.FromArgb(14, 165, 233),
                TextPrimary = Color.FromArgb(241, 245, 249),
                TextSecondary = Color.FromArgb(148, 163, 184),
                GridAlternate = Color.FromArgb(25, 32, 48)
            };
        }

        private static ShihabCustomThemePreset TryLoadCustomTomlTheme(string themeName, string baseDataDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(themeName)) return null;
                string safeStem = ShihabProcessAndPathSafety.SanitizeFileName(themeName, "");
                if (string.IsNullOrEmpty(safeStem) ||
                    themeName.IndexOf('\\') >= 0 ||
                    themeName.IndexOf('/') >= 0 ||
                    themeName.IndexOf("..", StringComparison.Ordinal) >= 0)
                {
                    return null;
                }

                string themesDir = Path.Combine(baseDataDir, "themes");
                if (!Directory.Exists(themesDir)) return null;

                string candidatePath;
                if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(themesDir, safeStem + ".toml", out candidatePath))
                {
                    return null;
                }
                if (!File.Exists(candidatePath)) return null;

                FileInfo fi = new FileInfo(candidatePath);
                if (fi.Length > MaxThemeTomlBytes) return null;

                ShihabCustomThemePreset preset = ResolveThemePalette("Obsidian Dark", "");
                preset.Name = themeName;
                string[] lines = File.ReadAllLines(candidatePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = ln.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = ln.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = ln.Substring(eq + 1).Trim().Trim('"', '\'');
                    Color parsed;
                    if (key == "name" && !string.IsNullOrWhiteSpace(val)) preset.Name = val;
                    else if ((key == "bg_main" || key == "background_main") && TryParseHexColor(val, out parsed)) preset.BackgroundMain = parsed;
                    else if ((key == "bg_card" || key == "background_card") && TryParseHexColor(val, out parsed)) preset.BackgroundCard = parsed;
                    else if ((key == "bg_sidebar" || key == "background_sidebar") && TryParseHexColor(val, out parsed)) preset.BackgroundSidebar = parsed;
                    else if ((key == "accent" || key == "accent_primary") && TryParseHexColor(val, out parsed)) preset.AccentPrimary = parsed;
                    else if (key == "text_primary" && TryParseHexColor(val, out parsed)) preset.TextPrimary = parsed;
                    else if (key == "text_secondary" && TryParseHexColor(val, out parsed)) preset.TextSecondary = parsed;
                    else if ((key == "grid_alt" || key == "grid_alternate") && TryParseHexColor(val, out parsed)) preset.GridAlternate = parsed;
                }

                // Enforce WCAG >= 4.5:1 contrast guard on primary text
                if (ComputeContrastRatio(preset.TextPrimary, preset.BackgroundMain) < 4.5)
                {
                    preset.TextPrimary = ComputeRelativeLuminance(preset.BackgroundMain) < 0.5
                        ? Color.FromArgb(241, 245, 249)
                        : Color.FromArgb(15, 23, 42);
                }
                if (ComputeContrastRatio(preset.TextSecondary, preset.BackgroundCard) < 3.0)
                {
                    preset.TextSecondary = ComputeRelativeLuminance(preset.BackgroundCard) < 0.5
                        ? Color.FromArgb(148, 163, 184)
                        : Color.FromArgb(71, 85, 105);
                }
                return preset;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.Black;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string s = hex.Trim().TrimStart('#');
            if (s.Length != 6) return false;
            int rgb;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) return false;
            color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        private static double ComputeRelativeLuminance(Color c)
        {
            double r = LinearizeChannel(c.R / 255.0);
            double g = LinearizeChannel(c.G / 255.0);
            double b = LinearizeChannel(c.B / 255.0);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double LinearizeChannel(double v)
        {
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static double ComputeContrastRatio(Color a, Color b)
        {
            double l1 = ComputeRelativeLuminance(a);
            double l2 = ComputeRelativeLuminance(b);
            double lighter = Math.Max(l1, l2);
            double darker = Math.Min(l1, l2);
            return (lighter + 0.05) / (darker + 0.05);
        }

        public static string ExtractLanguageIsoCode(string languageSetting)
        {
            if (string.IsNullOrWhiteSpace(languageSetting)) return "en";
            int dash = languageSetting.IndexOf('-');
            string code = (dash > 0 ? languageSetting.Substring(0, dash) : languageSetting).Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(code) ? "en" : code;
        }

        public static bool IsRightToLeftLanguage(string languageSetting)
        {
            string iso = ExtractLanguageIsoCode(languageSetting);
            return iso == "ar" || iso == "he" || iso == "fa" || iso == "ur";
        }

        public static string TranslateUiString(string languageSetting, string key, string fallbackEnglish)
        {
            string iso = ExtractLanguageIsoCode(languageSetting);
            if (iso == "en" || string.IsNullOrEmpty(key)) return fallbackEnglish;
            Dictionary<string, string> langMap;
            string translated;
            if (UiTranslations.TryGetValue(iso, out langMap) && langMap.TryGetValue(key, out translated))
            {
                return translated;
            }
            return fallbackEnglish;
        }
    }
}

