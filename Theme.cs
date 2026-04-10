using System.Runtime.InteropServices;

namespace JaneERP
{
    /// <summary>
    /// Jvnction Jane dark theme — deep black background · violet neon accents · teal highlights.
    /// </summary>
    public static class Theme
    {
        // ── Borderless / drag support ─────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, nint wParam, nint lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION        = 0x2;

        public static void MakeBorderless(Form form)
        {
            form.FormBorderStyle = FormBorderStyle.None;
            form.MouseDown += FormDrag;
        }

        public static void MakeDraggable(Form form, Control target)
        {
            if (target is Button) return;
            target.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
            foreach (Control child in target.Controls)
                MakeDraggable(form, child);
        }

        private static void FormDrag(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && sender is Form f)
            {
                ReleaseCapture();
                SendMessage(f.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        public static Button AddCloseButton(Form form)
        {
            var btn = new Button
            {
                Text      = "×",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                Size      = new Size(32, 28),
                BackColor = Color.FromArgb(160, 30, 20),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                TabStop   = false
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Danger;
            btn.Click += (_, _) => form.Close();
            form.Controls.Add(btn);

            void Position()
            {
                btn.Location = new Point(form.ClientSize.Width - btn.Width - 2, 2);
                btn.BringToFront();
            }

            // Subscribe to events AND position immediately — handles being called from a Load
            // handler (where the Load event has already fired) as well as from constructors.
            form.Load   += (_, _) => Position();
            form.Resize += (_, _) => Position();
            Position();   // always run now; BringToFront is safe even before handle creation
            return btn;
        }

        // ── Borderless resize support ─────────────────────────────────────────────
        private class ResizableHelper : NativeWindow
        {
            private const int WM_NCHITTEST   = 0x0084;
            private const int HTLEFT         = 10, HTRIGHT       = 11;
            private const int HTTOP          = 12, HTTOPLEFT     = 13, HTTOPRIGHT    = 14;
            private const int HTBOTTOM       = 15, HTBOTTOMLEFT  = 16, HTBOTTOMRIGHT = 17;
            private const int BorderWidth    = 6;
            private readonly Form _form;

            internal ResizableHelper(Form form)
            {
                _form = form;
                AssignHandle(form.Handle);
                form.Disposed += (_, _) => ReleaseHandle();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    var p = _form.PointToClient(Cursor.Position);
                    int w = _form.ClientSize.Width, h = _form.ClientSize.Height;
                    bool l = p.X <= BorderWidth, r = p.X >= w - BorderWidth;
                    bool t = p.Y <= BorderWidth, b = p.Y >= h - BorderWidth;
                    if (t && l)  { m.Result = (IntPtr)HTTOPLEFT;     return; }
                    if (t && r)  { m.Result = (IntPtr)HTTOPRIGHT;    return; }
                    if (b && l)  { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                    if (b && r)  { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    if (l)       { m.Result = (IntPtr)HTLEFT;        return; }
                    if (r)       { m.Result = (IntPtr)HTRIGHT;       return; }
                    if (t)       { m.Result = (IntPtr)HTTOP;         return; }
                    if (b)       { m.Result = (IntPtr)HTBOTTOM;      return; }
                }
                base.WndProc(ref m);
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, ResizableHelper>
            _resizeHelpers = new();

        public static void MakeResizable(Form form)
        {
            void Attach()
            {
                var helper = new ResizableHelper(form);
                _resizeHelpers.AddOrUpdate(form, helper);
            }
            if (form.IsHandleCreated) Attach();
            else form.HandleCreated += (_, _) => Attach();
        }

        // ── Core palette ─────────────────────────────────────────────────────────
        // Deep black base with violet neon accent
        public static readonly Color Background    = Color.FromArgb(8,   10,  20);   // #080A14
        public static readonly Color Surface       = Color.FromArgb(14,  16,  30);   // #0E101E
        public static readonly Color Header        = Color.FromArgb(5,   6,   14);   // #05060E
        public static readonly Color Gold          = Color.FromArgb(155, 55,  220);  // #9B37DC — violet accent
        public static readonly Color GoldDark      = Color.FromArgb(105, 25,  165);  // #6919A5 — deep violet
        public static readonly Color Teal          = Color.FromArgb(32,  184, 204);  // #20B8CC — keep teal
        public static readonly Color TextPrimary   = Color.FromArgb(234, 234, 234);  // #EAEAEA
        public static readonly Color TextSecondary = Color.FromArgb(170, 150, 200);  // #AA96C8 — soft lavender
        public static readonly Color TextMuted     = Color.FromArgb(90,  75,  120);  // #5A4B78 — muted violet
        public static readonly Color Border        = Color.FromArgb(55,  25,  90);   // #37195A — dark violet
        public static readonly Color InputBg       = Color.FromArgb(6,   7,   16);   // #060710
        public static readonly Color RowAlt        = Color.FromArgb(11,  13,  24);   // #0B0D18
        public static readonly Color Selected      = Color.FromArgb(60,  20,  100);  // #3C1464 — violet selection
        public static readonly Color Danger        = Color.FromArgb(200, 60,  50);   // #C83C32

        /// <summary>Semi-transparent glow colour used for neon border effects.</summary>
        public static readonly Color GlowOuter = Color.FromArgb(90, 155, 55, 220);  // 35% alpha violet

        /// <summary>Recursively apply the dark theme to a control and all its children.</summary>
        public static void Apply(Control root)
        {
            ApplyOne(root);
            foreach (Control c in root.Controls)
                Apply(c);
        }

        private static void ApplyOne(Control c)
        {
            switch (c)
            {
                case Form f:
                    f.BackColor = Background;
                    f.ForeColor = TextPrimary;
                    break;

                case Panel p:
                    p.BackColor = p.Tag as string == "header" ? Header : Surface;
                    p.ForeColor = TextPrimary;
                    break;

                case GroupBox grp:
                    grp.BackColor = Surface;
                    grp.ForeColor = TextSecondary;
                    break;

                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    lbl.ForeColor = TextPrimary;
                    break;

                case TextBox txt:
                    txt.BackColor   = InputBg;
                    txt.ForeColor   = TextPrimary;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case Button btn:
                    if (btn.UseVisualStyleBackColor)
                        StyleButton(btn);
                    break;

                case DataGridView dgv:
                    StyleGrid(dgv);
                    break;

                case ComboBox cbo:
                    cbo.BackColor = InputBg;
                    cbo.ForeColor = TextPrimary;
                    cbo.FlatStyle = FlatStyle.Flat;
                    break;

                case ListBox lst:
                    lst.BackColor = InputBg;
                    lst.ForeColor = TextPrimary;
                    break;

                case CheckBox chk:
                    chk.ForeColor = TextPrimary;
                    chk.BackColor = Color.Transparent;
                    break;

                case DateTimePicker dtp:
                    dtp.BackColor               = InputBg;
                    dtp.ForeColor               = TextPrimary;
                    dtp.CalendarForeColor       = TextPrimary;
                    dtp.CalendarMonthBackground = Surface;
                    dtp.CalendarTitleBackColor  = Header;
                    dtp.CalendarTitleForeColor  = Gold;
                    break;

                case PictureBox:
                    break;

                default:
                    c.BackColor = Background;
                    c.ForeColor = TextPrimary;
                    break;
            }
        }

        /// <summary>Violet-filled primary action button.</summary>
        public static void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor        = Gold;
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = GoldDark;
            btn.BackColor = Gold;
            btn.ForeColor = Color.White;
            btn.Cursor    = Cursors.Hand;
        }

        /// <summary>Outlined secondary button (no fill).</summary>
        public static void StyleSecondaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor        = Border;
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 15, 50);
            btn.BackColor = Surface;
            btn.ForeColor = TextPrimary;
            btn.Cursor    = Cursors.Hand;
        }

        /// <summary>Apply dark theme to a DataGridView.</summary>
        public static void StyleGrid(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.BackgroundColor           = InputBg;
            dgv.ForeColor                 = TextPrimary;
            dgv.GridColor                 = Border;
            dgv.BorderStyle               = BorderStyle.None;

            dgv.DefaultCellStyle.BackColor          = Surface;
            dgv.DefaultCellStyle.ForeColor          = TextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = Selected;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Header;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Gold;
            dgv.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersBorderStyle               = DataGridViewHeaderBorderStyle.Single;
        }

        /// <summary>Path to the Jane mascot image.</summary>
        public static string MascotImagePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "jane_mascot.jpg");

        /// <summary>Default path to the horizontal Jvnction logo.</summary>
        public static string DefaultLogoPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo_horizontal.png");

        public static PictureBox CreateLogoBadge(int x, int y, int width = 160, int height = 44)
        {
            var pb = new PictureBox
            {
                Location  = new Point(x, y),
                Size      = new Size(width, height),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Padding   = new Padding(4),
                Cursor    = Cursors.Default
            };

            try
            {
                var path = AppSettings.Current.LogoPath;
                if (File.Exists(path))
                    pb.Image = Image.FromFile(path);
                else if (File.Exists(DefaultLogoPath))
                    pb.Image = Image.FromFile(DefaultLogoPath);
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[Theme.CreateLogoBadge]: {ex.Message}"); }

            return pb;
        }
    }
}
