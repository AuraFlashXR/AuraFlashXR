// bin\launcher.exe: the front door, started by the stub AuraFlashXR.exe (the only file in the package root).
// Bento Grid layout (refonte 2026-11-01): Hero Card (Streamer) + grille 2x2 (Stats, Paramètres, Diagnostics, Wizard) + Toolbox OpenXR.
// Logique métier conservée à l'identique (presets, firewall, host, ADB, APK, SteamVR, langue, test flow).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AuraFlashXR
{
    /// Carte Bento : icône vectorielle GDI+ + titre + description, halo au survol, clavier (Entrée/Espace).
    public class Card : Panel
    {
        public bool IsPrimary;
        public bool IsHovered;
        public string IconKey = "";
        public string TitleText = "";
        public string DescText = "";
        public Action Chosen;   // clic métier

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;   // le parent peint le ciel : les coins arrondis ne gardent aucun fond sombre
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        public void PerformClick() => Chosen?.Invoke();
        protected override bool IsInputKey(Keys k) => k == Keys.Enter || k == Keys.Space || base.IsInputKey(k);
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { PerformClick(); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnMouseEnter(EventArgs e) { IsHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { IsHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) PerformClick(); base.OnMouseUp(e); }

        // Palette propre à chaque carte : la principale est ambre (éclair), les autres prennent la teinte de leur outil.
        public Color Accent = Ui.AccentLight;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            // les coins hors du tracé arrondi reprennent le ciel de la fenêtre : plus de triangles sombres
            if (Parent is LauncherForm host) host.PaintSkyBand(g, Bounds);
            else g.Clear(Parent?.BackColor ?? Ui.DarkBg);

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);

            // Carte : surface Bento pleine, sans effet de verre.
            using (var path = Ui.RoundRect(rect, 14))
            {
                using (var b = new SolidBrush(IsHovered ? Color.FromArgb(0x26, 0x2C, 0x3C) : Ui.DarkCard))
                    g.FillPath(b, path);

                // Contour : hero en dégradé orange → rouge, les autres liseré de verre froid.
                if (IsPrimary || IsHovered)
                {
                    using (var bp = new LinearGradientBrush(rect, IsHovered ? Ui.AccentLight : Color.FromArgb(0xFF, 0x8A, 0x1E), Color.FromArgb(0xE8, 0x3C, 0x2A), LinearGradientMode.Horizontal))
                    using (var pen = new Pen(bp, 1.4f)) g.DrawPath(pen, path);
                }
                else
                {
                    using (var pen = new Pen(Color.FromArgb(0x3A, 0x9F, 0xB4, 0xD0), 1.2f)) g.DrawPath(pen, path);
                }

                g.SetClip(path);

                // Icône nue, fine, à gauche (pas de badge).
                var iconColor = IsHovered ? Ui.Gold : Color.FromArgb(0xF2, 0x96, 0x22);
                const int iconSize = 60;
                const int iconX = 22;
                DrawIcon(g, new Rectangle(iconX, (Height - iconSize) / 2 - (IsPrimary ? 0 : 2), iconSize, iconSize), iconColor);

                // titre + description : titre 15 px gras, description 12 px gris clair sur 2 lignes.
                int textX = iconX + iconSize + 22;
                int textW = Width - textX - 46;
                var sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                var sfDesc = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit };
                using (var tf = Ui.BrandFont(IsPrimary ? 15.5f : 14f, FontStyle.Bold))
                using (var tb = new SolidBrush(IsHovered ? Color.White : Color.FromArgb(0xF6, 0xF8, 0xFB)))
                {
                    var ts = g.MeasureString(TitleText, tf, new PointF(0, 0), sfTitle);
                    float titleY = IsPrimary ? 20 : 18;
                    g.DrawString(TitleText, tf, tb, new RectangleF(textX, titleY, textW, ts.Height + 2), sfTitle);

                    using (var df = Ui.BrandFont(IsPrimary ? 11f : 10.5f, FontStyle.Bold))
                    using (var db = new SolidBrush(Color.FromArgb(0xC9, 0xD1, 0xDD)))
                    {
                        float descY = titleY + ts.Height - 1;
                        g.DrawString(DescText, df, db, new RectangleF(textX, descY, textW, Height - descY - 8), sfDesc);
                    }
                }

                if (IsPrimary)
                {
                    // Carte diffuseur : pastille « DÉMARRER ▶ » ambre, comme le bouton de lancement.
                    string startText = Lang.Get("Launcher.Start", "DÉMARRER");
                    using (var sf = Ui.BrandFont(11f, FontStyle.Bold))
                    {
                        var size = g.MeasureString(startText, sf);
                        float pw = size.Width + 38, ph = 30;
                        var pill = new RectangleF(Width - pw - 18, Height / 2f - ph / 2f, pw, ph);   // 10 % a gauche, puis 5 % + 5 % a droite
                        using (var pp = new GraphicsPath())
                        {
                            float d = ph;
                            pp.AddArc(pill.Left, pill.Top, d, d, 90, 180);
                            pp.AddArc(pill.Right - d, pill.Top, d, d, 270, 180);
                            pp.CloseFigure();
                            using (var pb = new LinearGradientBrush(pill, IsHovered ? Ui.AccentLight : Color.FromArgb(0xFF, 0xB3, 0x1A), Color.FromArgb(0xE8, 0x6A, 0x0A), LinearGradientMode.Horizontal))
                                g.FillPath(pb, pp);
                        }
                        using (var tb2 = new SolidBrush(Color.FromArgb(0x14, 0x10, 0x08)))
                            g.DrawString(startText, sf, tb2, pill.Left + 15, pill.Top + (ph - size.Height) / 2f);
                        // triangle lecture
                        float tx = pill.Right - 20, ty = pill.Top + ph / 2f;
                        using (var tri = new SolidBrush(Color.FromArgb(0x14, 0x10, 0x08)))
                            g.FillPolygon(tri, new[] { new PointF(tx, ty - 6), new PointF(tx, ty + 6), new PointF(tx + 9, ty) });
                    }
                }
                else
                {
                    // chevron › à droite
                    using (var cf = Ui.BrandFont(30f, FontStyle.Bold))
                    using (var cb = new SolidBrush(IsHovered ? Ui.AccentLight : Color.FromArgb(0xB4, 0xBE, 0xCB)))
                        g.DrawString("›", cf, cb, Width - 38, Height / 2f - 28);
                }

                g.ResetClip();
            }
        }

        /// Ombre portée douce sous la carte (relief) : dégradés superposés vers le bas, sans flou GDI+.
        void PaintDropShadow(Graphics g, Rectangle rect, int radius)
        {
            var saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int layers = IsHovered ? 8 : 6;
            for (int i = layers; i >= 1; i--)
            {
                int a = (int)((IsHovered ? 0x14 : 0x10) * (1f - (float)(i - 1) / layers));
                if (a <= 0) continue;
                var r = new Rectangle(rect.X - i, rect.Y - i + 3, rect.Width + i * 2, rect.Height + i * 2);
                using (var p = Ui.RoundRect(r, radius + i))
                using (var pen = new Pen(Color.FromArgb(a, 0x00, 0x00, 0x00), 1f))
                    g.DrawPath(pen, p);
            }
            g.Restore(saved);
        }

        /// Effet verre 3D : reflet diagonal en haut, liseré clair sur l'arête supérieure, ombre interne en bas.
        void PaintGlass(Graphics g, GraphicsPath path, Rectangle rect)
        {
            var saved = g.Save();
            g.SetClip(path);

            // 1. reflet froid en haut : dégradé blanc bleuté qui s'éteint sur la moitié supérieure
            using (var sheen = new LinearGradientBrush(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height),
                                                       Color.FromArgb(IsHovered ? 0x2A : 0x1E, 0xE8, 0xF0, 0xFF),
                                                       Color.FromArgb(0, 0xE8, 0xF0, 0xFF),
                                                       LinearGradientMode.Vertical))
            {
                var blend = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(IsHovered ? 0x2A : 0x1E, 0xE8, 0xF0, 0xFF),
                        Color.FromArgb(IsHovered ? 0x0A : 0x06, 0xE8, 0xF0, 0xFF),
                        Color.FromArgb(0, 0xE8, 0xF0, 0xFF)
                    },
                    Positions = new[] { 0f, 0.35f, 1f }
                };
                sheen.InterpolationColors = blend;
                g.FillRectangle(sheen, rect);
            }

            // 2. liseré net sur l'arête supérieure (1 px, s'estompe vers les coins)
            using (var edge = new LinearGradientBrush(new Rectangle(rect.X, rect.Y, rect.Width, 2),
                                                      Color.FromArgb(IsHovered ? 0xB4 : 0x8C, 0xFF, 0xFF, 0xFF),
                                                      Color.FromArgb(0, 0xFF, 0xFF, 0xFF),
                                                      LinearGradientMode.Vertical))
                g.FillRectangle(edge, new Rectangle(rect.X + 14, rect.Y + 1, rect.Width - 28, 1));

            // 3. ombre douce en bas (le verre se pose sur la surface)
            int shadeH = Math.Max(6, rect.Height / 4);
            using (var shade = new LinearGradientBrush(new Rectangle(rect.X, rect.Bottom - shadeH, rect.Width, shadeH),
                                                       Color.FromArgb(0, 0x00, 0x00, 0x00),
                                                       Color.FromArgb(IsHovered ? 0x30 : 0x22, 0x00, 0x00, 0x00),
                                                       LinearGradientMode.Vertical))
                g.FillRectangle(shade, new Rectangle(rect.X, rect.Bottom - shadeH, rect.Width, shadeH));

            // 4. relief bombé : lumière froide en haut à gauche, ombre en bas à droite (verre convexe)
            using (var dome = new PathGradientBrush(path))
            {
                dome.CenterPoint = new PointF(rect.X + rect.Width * 0.32f, rect.Y + rect.Height * 0.12f);
                dome.CenterColor = Color.FromArgb(IsHovered ? 0x1C : 0x12, 0xE8, 0xF0, 0xFF);
                dome.SurroundColors = new[] { Color.FromArgb(0, 0xE8, 0xF0, 0xFF) };
                g.FillPath(dome, path);
            }

            // 5. ombre interne sur les arêtes gauche/droite (épaisseur du verre)
            using (var lEdge = new LinearGradientBrush(new Rectangle(rect.X, rect.Y, 10, rect.Height),
                                                       Color.FromArgb(0x14, 0x00, 0x00, 0x00),
                                                       Color.FromArgb(0, 0x00, 0x00, 0x00), LinearGradientMode.Horizontal))
                g.FillRectangle(lEdge, new Rectangle(rect.X, rect.Y, 10, rect.Height));
            using (var rEdge = new LinearGradientBrush(new Rectangle(rect.Right - 10, rect.Y, 10, rect.Height),
                                                       Color.FromArgb(0, 0x00, 0x00, 0x00),
                                                       Color.FromArgb(0x1A, 0x00, 0x00, 0x00), LinearGradientMode.Horizontal))
                g.FillRectangle(rEdge, new Rectangle(rect.Right - 10, rect.Y, 10, rect.Height));

            g.Restore(saved);
        }

        /// Liquid Glass : lentille spéculaire (grande lumière froide en haut à gauche), bande de réfraction en bas,
        /// rim light sur tout le pourtour et ombre de contact sous l'arête inférieure.
        void PaintLiquidGlass(Graphics g, GraphicsPath path, Rectangle rect)
        {
            var saved = g.Save();
            g.SetClip(path);

            // 1. lentille : halo blanc bleuté intense au tiers supérieur gauche, décroissance douce
            using (var lens = new PathGradientBrush(path))
            {
                lens.CenterPoint = new PointF(rect.X + rect.Width * 0.30f, rect.Y + rect.Height * 0.22f);
                lens.CenterColor = Color.FromArgb(IsHovered ? 0x58 : 0x44, 0xF2, 0xF7, 0xFF);
                lens.SurroundColors = new[] { Color.FromArgb(0, 0xF2, 0xF7, 0xFF) };
                lens.FocusScales = new PointF(0.30f, 0.22f);
                g.FillPath(lens, path);
            }

            // 1b. réfraction latérale : l'arête droite capte la lumière et la renvoie en frange froide
            using (var edge = new LinearGradientBrush(new Rectangle(rect.Right - rect.Width / 4, rect.Y, rect.Width / 4, rect.Height),
                                                      Color.FromArgb(0, 0x9F, 0xC8, 0xFF),
                                                      Color.FromArgb(IsHovered ? 0x2A : 0x1E, 0x9F, 0xC8, 0xFF), LinearGradientMode.Horizontal))
            {
                var eb = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 0x9F, 0xC8, 0xFF),
                        Color.FromArgb(IsHovered ? 0x14 : 0x0E, 0x9F, 0xC8, 0xFF),
                        Color.FromArgb(IsHovered ? 0x2A : 0x1E, 0x9F, 0xC8, 0xFF)
                    },
                    Positions = new[] { 0f, 0.6f, 1f }
                };
                edge.InterpolationColors = eb;
                g.FillRectangle(edge, new Rectangle(rect.Right - rect.Width / 4, rect.Y, rect.Width / 4, rect.Height));
            }

            // 1c. reflet diagonal : bande de lumière oblique traversant le verre
            using (var shine = new GraphicsPath())
            {
                float w = rect.Width, h = rect.Height;
                shine.AddPolygon(new[]
                {
                    new PointF(rect.X + w * 0.06f, rect.Y),
                    new PointF(rect.X + w * 0.30f, rect.Y),
                    new PointF(rect.X + w * 0.12f, rect.Bottom),
                    new PointF(rect.X - w * 0.12f, rect.Bottom)
                });
                using (var sb = new LinearGradientBrush(rect,
                                                        Color.FromArgb(IsHovered ? 0x3A : 0x28, 0xFF, 0xFF, 0xFF),
                                                        Color.FromArgb(0, 0xFF, 0xFF, 0xFF), LinearGradientMode.Horizontal))
                {
                    var cb = new ColorBlend(3)
                    {
                        Colors = new[]
                        {
                            Color.FromArgb(0, 0xFF, 0xFF, 0xFF),
                            Color.FromArgb(IsHovered ? 0x3A : 0x28, 0xFF, 0xFF, 0xFF),
                            Color.FromArgb(0, 0xFF, 0xFF, 0xFF)
                        },
                        Positions = new[] { 0f, 0.18f, 1f }
                    };
                    sb.InterpolationColors = cb;
                    g.FillPath(sb, shine);
                }
            }

            // 2. réfraction : le bas du verre capte le ciel et le renvoie en bleu froid, juste avant l'ombre
            int bandH = Math.Max(10, rect.Height / 3);
            using (var band = new LinearGradientBrush(new Rectangle(rect.X, rect.Bottom - bandH, rect.Width, bandH),
                                                      Color.FromArgb(0, 0x8C, 0xB4, 0xFF),
                                                      Color.FromArgb(IsHovered ? 0x30 : 0x22, 0x8C, 0xB4, 0xFF), LinearGradientMode.Vertical))
            {
                var blend = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 0x8C, 0xB4, 0xFF),
                        Color.FromArgb(IsHovered ? 0x1E : 0x14, 0x8C, 0xB4, 0xFF),
                        Color.FromArgb(IsHovered ? 0x30 : 0x22, 0x8C, 0xB4, 0xFF)
                    },
                    Positions = new[] { 0f, 0.7f, 1f }
                };
                band.InterpolationColors = blend;
                g.FillRectangle(band, new Rectangle(rect.X, rect.Bottom - bandH, rect.Width, bandH));
            }

            // 3. ombre de contact : le verre épais absorbe la lumière juste sous son arête basse
            int contactH = Math.Max(4, rect.Height / 8);
            using (var contact = new LinearGradientBrush(new Rectangle(rect.X, rect.Bottom - contactH, rect.Width, contactH),
                                                         Color.FromArgb(0, 0, 0, 0),
                                                         Color.FromArgb(IsHovered ? 0x3C : 0x2C, 0x00, 0x00, 0x00), LinearGradientMode.Vertical))
                g.FillRectangle(contact, new Rectangle(rect.X, rect.Bottom - contactH, rect.Width, contactH));

            // 4. rim light épais : arête claire sur tout le pourtour (haut intense, bas résiduel)
            using (var rim = new Pen(new LinearGradientBrush(rect,
                                                             Color.FromArgb(IsHovered ? 0xD8 : 0xB4, 0xFF, 0xFF, 0xFF),
                                                             Color.FromArgb(IsHovered ? 0x40 : 0x26, 0xFF, 0xFF, 0xFF),
                                                             LinearGradientMode.Vertical), 2.2f))
                g.DrawPath(rim, path);

            // 4b. arête interne sombre : épaisseur du verre (relief 3D)
            using (var inner = Ui.RoundRect(new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6), 9))
            using (var innerPen = new Pen(Color.FromArgb(IsHovered ? 0x30 : 0x20, 0x00, 0x00, 0x00), 1.6f))
                g.DrawPath(innerPen, inner);

            // 5. éclat spéculaire diffus : halo doux en haut à gauche, fondu sur toute la largeur
            using (var spec = new LinearGradientBrush(new Rectangle(rect.X + 10, rect.Y + 2, rect.Width - 20, 10),
                                                      Color.FromArgb(IsHovered ? 0x70 : 0x50, 0xFF, 0xFF, 0xFF),
                                                      Color.FromArgb(0, 0xFF, 0xFF, 0xFF), LinearGradientMode.Horizontal))
            {
                var sb = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(IsHovered ? 0x70 : 0x50, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(IsHovered ? 0x1E : 0x14, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(0, 0xFF, 0xFF, 0xFF)
                    },
                    Positions = new[] { 0f, 0.45f, 1f }
                };
                spec.InterpolationColors = sb;
                g.FillRectangle(spec, new Rectangle(rect.X + 10, rect.Y + 2, rect.Width - 20, 10));
            }

            g.Restore(saved);
        }

        /// Icônes vectorielles dessinées : contour néon double trait (cœur clair + halo coloré).
        void DrawIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var saved = g.Save();
            var c = new PointF(r.Left + r.Width / 2f, r.Top + r.Height / 2f);
            var core = Color.FromArgb(
                Math.Min(255, color.R + 70),
                Math.Min(255, color.G + 70),
                Math.Min(255, color.B + 70));

            // Néon : trait extérieur coloré puis cœur clair net.
            using (var path = BuildIconPath(r))
            {
                using (var neon = new Pen(Color.FromArgb(150, color), 3.4f))
                {
                    neon.LineJoin = LineJoin.Round;
                    neon.StartCap = LineCap.Round;
                    neon.EndCap = LineCap.Round;
                    g.DrawPath(neon, path);
                }
                using (var pen = new Pen(core, 1.5f))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, path);
                }
            }

            // Détails pleins (petits points/barres) propres à certaines icônes.
            using (var brush = new SolidBrush(core))
            {
                switch (IconKey)
                {
                    case "Settings":
                        g.FillEllipse(brush, c.X - 2.5f, c.Y - 2.5f, 5, 5);
                        break;
                    case "Wizard":
                        g.FillRectangle(brush, r.Left + r.Width * 0.10f, r.Top + r.Height * 0.46f, r.Width * 0.16f, r.Height * 0.30f);
                        g.FillRectangle(brush, r.Left + r.Width * 0.74f, r.Top + r.Height * 0.46f, r.Width * 0.16f, r.Height * 0.30f);
                        break;
                }
            }

            g.Restore(saved);
        }

        /// Tracé de contour (non rempli) de l'icône courante.
        GraphicsPath BuildIconPath(Rectangle r)
        {
            var path = new GraphicsPath();
            float w = r.Width, h = r.Height;
            switch (IconKey)
            {
                case "Streamer": // éclair
                    path.AddPolygon(new[] {
                        new PointF(r.Left + w * 0.58f, r.Top + h * 0.08f),
                        new PointF(r.Left + w * 0.24f, r.Top + h * 0.56f),
                        new PointF(r.Left + w * 0.46f, r.Top + h * 0.56f),
                        new PointF(r.Left + w * 0.38f, r.Top + h * 0.92f),
                        new PointF(r.Left + w * 0.76f, r.Top + h * 0.42f),
                        new PointF(r.Left + w * 0.54f, r.Top + h * 0.42f),
                    });
                    path.CloseFigure();
                    break;
                case "Stats": // graphique en barres
                    path.AddRectangle(new RectangleF(r.Left + w * 0.14f, r.Top + h * 0.56f, w * 0.16f, h * 0.36f));
                    path.AddRectangle(new RectangleF(r.Left + w * 0.42f, r.Top + h * 0.36f, w * 0.16f, h * 0.56f));
                    path.AddRectangle(new RectangleF(r.Left + w * 0.70f, r.Top + h * 0.14f, w * 0.16f, h * 0.78f));
                    break;
                case "Settings": // engrenage contour : anneau + 8 dents
                    var ring = Rectangle.Inflate(r, -r.Width / 4, -r.Height / 4);
                    var c = new PointF(r.Left + w / 2f, r.Top + h / 2f);
                    for (int i = 0; i < 8; i++)
                    {
                        var a = i * 45.0 * Math.PI / 180.0;
                        path.AddLine(
                            c.X + (float)(ring.Width / 2.0 * Math.Cos(a)),
                            c.Y + (float)(ring.Height / 2.0 * Math.Sin(a)),
                            c.X + (float)(ring.Width / 2.0 * 1.45 * Math.Cos(a)),
                            c.Y + (float)(ring.Height / 2.0 * 1.45 * Math.Sin(a)));
                    }
                    path.AddEllipse(ring);
                    break;
                case "Diagnostics": // cœur + pouls
                    path.AddBezier(r.Left + w * 0.50f, r.Top + h * 0.90f,
                                   r.Left + w * 0.06f, r.Top + h * 0.62f,
                                   r.Left + w * 0.06f, r.Top + h * 0.28f,
                                   r.Left + w * 0.30f, r.Top + h * 0.20f);
                    path.AddBezier(r.Left + w * 0.30f, r.Top + h * 0.20f,
                                   r.Left + w * 0.44f, r.Top + h * 0.16f,
                                   r.Left + w * 0.50f, r.Top + h * 0.30f,
                                   r.Left + w * 0.50f, r.Top + h * 0.36f);
                    path.AddBezier(r.Left + w * 0.50f, r.Top + h * 0.36f,
                                   r.Left + w * 0.50f, r.Top + h * 0.30f,
                                   r.Left + w * 0.56f, r.Top + h * 0.16f,
                                   r.Left + w * 0.70f, r.Top + h * 0.20f);
                    path.AddBezier(r.Left + w * 0.70f, r.Top + h * 0.20f,
                                   r.Left + w * 0.94f, r.Top + h * 0.28f,
                                   r.Left + w * 0.94f, r.Top + h * 0.62f,
                                   r.Left + w * 0.50f, r.Top + h * 0.90f);
                    path.StartFigure();
                    path.AddLine(r.Left + w * 0.20f, r.Top + h * 0.52f, r.Left + w * 0.38f, r.Top + h * 0.52f);
                    path.AddLine(r.Left + w * 0.38f, r.Top + h * 0.52f, r.Left + w * 0.46f, r.Top + h * 0.34f);
                    path.AddLine(r.Left + w * 0.46f, r.Top + h * 0.34f, r.Left + w * 0.56f, r.Top + h * 0.70f);
                    path.AddLine(r.Left + w * 0.56f, r.Top + h * 0.70f, r.Left + w * 0.64f, r.Top + h * 0.52f);
                    path.AddLine(r.Left + w * 0.64f, r.Top + h * 0.52f, r.Left + w * 0.80f, r.Top + h * 0.52f);
                    break;
                case "Wizard": // casque VR : arceau + 2 écouteurs
                    path.AddArc(r.Left + w * 0.14f, r.Top + h * 0.08f, w * 0.72f, h * 0.78f, 180, 180);
                    break;
                case "Wrench": // clé à molette contour
                    Icons.AddWrenchOutline(path);
                    var m = new Matrix();
                    m.Translate(r.Left, r.Top);
                    m.Scale(w / 24f, h / 24f);
                    path.Transform(m);
                    m.Dispose();
                    break;
                default: // chevron
                    path.AddLine(r.Left + w * 0.35f, r.Top + h * 0.25f, r.Left + w * 0.65f, r.Top + h * 0.50f);
                    path.AddLine(r.Left + w * 0.65f, r.Top + h * 0.50f, r.Left + w * 0.35f, r.Top + h * 0.75f);
                    break;
            }
            return path;
        }
    }

    /// Icônes vectorielles partagées.
    internal static class Icons
    {
        /// Clé à molette en contour : tête ouverte, manche diagonal, fourche. Coordonnées 24 × 24.
        internal static void AddWrenchOutline(GraphicsPath path)
        {
            path.AddArc(1.2f, 1.2f, 12.6f, 12.6f, 250f, 290f);
            path.AddArc(4.6f, 4.6f, 5.8f, 5.8f, 540f, -290f);
            path.CloseFigure();
            path.AddLine(9.6f, 9.6f, 16.4f, 16.4f);
            path.StartFigure();
            path.AddPolygon(new[]
            {
                new PointF(14.6f, 15.6f), new PointF(17.0f, 13.2f), new PointF(22.4f, 18.6f),
                new PointF(22.4f, 22.4f), new PointF(18.6f, 22.4f),
            });
            path.AddPolygon(new[]
            {
                new PointF(18.6f, 13.2f), new PointF(22.4f, 13.2f), new PointF(22.4f, 17.0f),
                new PointF(20.0f, 15.6f),
            });
        }

        /// Clé à molette pleine : tête ouverte (anneau fendu) en haut à gauche, manche diagonal, fourche en bas à droite.
        /// Dessinée en polygones remplis pour rester nette à toutes les tailles.
        internal static void DrawWrench(Graphics g, RectangleF r, Color color)
        {
            var saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.Left, r.Top);
            g.ScaleTransform(r.Width / 24f, r.Height / 24f);

            using (var brush = new SolidBrush(color))
            using (var head = new GraphicsPath())
            {
                // Tête : anneau ouvert vers le haut-gauche (fente de 70°).
                var outer = new RectangleF(1.2f, 1.2f, 12.6f, 12.6f);
                var inner = new RectangleF(4.6f, 4.6f, 5.8f, 5.8f);
                head.AddArc(outer, 250f, 290f);
                head.AddArc(inner, 540f, -290f);
                head.CloseFigure();
                g.FillPath(brush, head);

                // Manche : barre diagonale à bouts arrondis.
                using (var shaft = new GraphicsPath())
                {
                    const float x1 = 9.6f, y1 = 9.6f, x2 = 16.4f, y2 = 16.4f, half = 1.9f;
                    var dx = x2 - x1; var dy = y2 - y1;
                    var len = (float)Math.Sqrt(dx * dx + dy * dy);
                    var nx = -dy / len * half; var ny = dx / len * half;
                    shaft.AddPolygon(new[]
                    {
                        new PointF(x1 + nx, y1 + ny), new PointF(x2 + nx, y2 + ny),
                        new PointF(x2 - nx, y2 - ny), new PointF(x1 - nx, y1 - ny),
                    });
                    g.FillPath(brush, shaft);
                    g.FillEllipse(brush, x1 - half, y1 - half, half * 2, half * 2);
                    g.FillEllipse(brush, x2 - half, y2 - half, half * 2, half * 2);
                }

                // Fourche : deux dents ouvertes vers le bas-droite.
                using (var fork = new GraphicsPath())
                {
                    fork.AddPolygon(new[]
                    {
                        new PointF(14.6f, 15.6f), new PointF(17.0f, 13.2f), new PointF(22.4f, 18.6f),
                        new PointF(22.4f, 22.4f), new PointF(18.6f, 22.4f),
                    });
                    fork.AddPolygon(new[]
                    {
                        new PointF(18.6f, 13.2f), new PointF(22.4f, 13.2f), new PointF(22.4f, 17.0f),
                        new PointF(20.0f, 15.6f),
                    });
                    g.FillPath(brush, fork);
                }
            }

            g.Restore(saved);
        }

    }

    public class LauncherForm : DpiForm
    {
        Image backImage;   // fond du launcher : embarqué (res/backv2.jpg) ou choisi dans le menu BACKGROUND

        class Tool { public string Key, Title, What, Exe, Args = ""; public bool StopsStreamer, Primary; }
        // Titles/Wheres are resolved lazily in MakeCard (Lang.Get with fallback) because
        // Lang.Init() runs in the LauncherForm constructor, AFTER this field initializer.
        static readonly Tool[] Tools;

        /// Lang.Init() runs AFTER field initializer → resolve titles/whats lazily in MakeCard.
        static LauncherForm()
        {
            Lang.Init();
            Tools = new Tool[] {
                new Tool { Key = "Streamer", Title = "Launcher.Tool.Streamer", Exe = @"bin\streamer.exe", Primary = true, What = "Launcher.Tool.StreamerWhat" },
                new Tool { Key = "Stats", Title = "Launcher.Tool.Stats", Exe = @"bin\performance.exe", What = "Launcher.Tool.StatsWhat" },
                new Tool { Key = "Settings", Title = "Launcher.Tool.Settings", Exe = @"bin\configure.exe", Args = "--settings", StopsStreamer = true, What = "Launcher.Tool.SettingsWhat" },
                new Tool { Key = "Diagnostics", Title = "Launcher.Tool.Diagnostics", Exe = @"bin\configure.exe", Args = "--diagnose", StopsStreamer = true, What = "Launcher.Tool.DiagnosticsWhat" },
                new Tool { Key = "Wizard", Title = "Launcher.Tool.Wizard", Exe = @"bin\configure.exe", StopsStreamer = true, What = "Launcher.Tool.WizardWhat" },
            };
        }

        // plus de Dock/AutoScroll — la Toolbox est positionnée en absolu, taille fixe, zéro scrollbar
        readonly FlowLayoutPanel sys = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Padding = new Padding(16, 12, 16, 12) };
        internal Control ToolboxControl => sys;
        readonly bool demoReady;
        FwReport firewall;           // filled in the background (PowerShell takes a few seconds)
        // bubble showing which configuration is currently loaded (same style as configure.exe)
        readonly Label configBubble = new Label { AutoSize = true, Font = Ui.Bold, Padding = new Padding(16, 7, 16, 7), Margin = new Padding(8, 2, 0, 2), BackColor = Color.FromArgb(0x10, 0x1A, 0x20), ForeColor = Color.FromArgb(0x8C, 0xF0, 0xC8) };
        bool configBubbleReadOnly;
        Label footerStatus;
        static readonly string[] ToolProcesses = { "streamer", "configure", "performance" };
        readonly Timer watch = new Timer { Interval = 500 };
        Process launched;
        bool testFlow;
        static readonly string FlowLogPath = Environment.GetEnvironmentVariable("AURAFLASHXR_TEST_FLOW_LOG");

        /// demoReady: the screenshot / harness state: everything in place, nothing checked.
        public LauncherForm(bool demoReady = false)
        {
            this.demoReady = demoReady;
            Lang.Init();
            Text = Lang.Get("Launcher.Title");
            UpdateConfigBubble();   // rafraichir la bulle preset apres un changement de langue
            Icon = Ui.AppIcon();
            Ui.LoadFontChoice();   // police + graisse choisies dans le menu POLICE, avant toute création de contrôle
            backImage = LoadBackground(new AppSettings().Background);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Body;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            BackColor = Color.FromArgb(0x0B, 0x0E, 0x14);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            ForeColor = Ui.DarkText;
            ClientSize = new Size(1180, 705);   // hauteur de la marge basse reduite de 10 %
            using (var rgn = Ui.RoundRect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), 18)) Region = new Region(rgn);

            sys.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // les coins hors du tracé arrondi reprennent le ciel de la fenêtre
                PaintSkyBand(g, sys.Bounds);
                PaintToolboxSurface(g, sys);
            };

            Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // coins arrondis de la fenêtre : tout ce qui dépasse du tracé reprend la couleur sombre
                using (var outer = new SolidBrush(BackColor)) g.FillRectangle(outer, ClientRectangle);
                using (var frame = Ui.RoundRect(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 18))
                {
                    var saved = g.Save();
                    g.SetClip(frame);
                    // fond : image back.png embarquée, étirée sur toute la fenêtre
                    if (backImage != null) g.DrawImage(backImage, ClientRectangle);
                    else using (var b = new SolidBrush(Ui.DarkBg)) g.FillRectangle(b, ClientRectangle);
                    g.Restore(saved);
                    using (var pen = new Pen(Color.FromArgb(0x26, 0x2C, 0x3C), 1f)) g.DrawPath(pen, frame);
                }
                // header et footer sont deja peints par le fond ci-dessus (ne pas redessiner l'image : elle se dupliquait)
            };
            BuildLayout();
            RefreshSystem();
            if (!demoReady) Shown += (s, e) => System.Threading.Tasks.Task.Run(() => Firewall.Analyze(Paths.Host)).ContinueWith(t => { try { BeginInvoke((Action)(() => { firewall = t.Result; RefreshSystem(); })); } catch { } });
            Ui.ScreenshotWhenShown(this, 800);
        }

        /// Liquid Glass (même recette que les cartes) : lentille spéculaire, réfraction froide, ombre de contact,
        /// rim light et éclat diffus — appliqué au fond de la Toolbox.
        static void PaintLiquidGlass(Graphics g, GraphicsPath path, Rectangle rect)
        {
            if (rect.Width < 40 || rect.Height < 14) return;
            var saved = g.Save();
            g.SetClip(path);

            // 1. lentille : halo blanc bleuté au tiers supérieur gauche
            using (var lens = new PathGradientBrush(path))
            {
                lens.CenterPoint = new PointF(rect.X + rect.Width * 0.30f, rect.Y + rect.Height * 0.22f);
                lens.CenterColor = Color.FromArgb(0x44, 0xF2, 0xF7, 0xFF);
                lens.SurroundColors = new[] { Color.FromArgb(0, 0xF2, 0xF7, 0xFF) };
                lens.FocusScales = new PointF(0.30f, 0.22f);
                g.FillPath(lens, path);
            }

            // 1b. réfraction latérale : frange froide sur l'arête droite
            using (var edge = new LinearGradientBrush(new Rectangle(rect.Right - rect.Width / 4, rect.Y, rect.Width / 4, rect.Height),
                                                      Color.FromArgb(0, 0x9F, 0xC8, 0xFF),
                                                      Color.FromArgb(0x1E, 0x9F, 0xC8, 0xFF), LinearGradientMode.Horizontal))
            {
                var eb = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 0x9F, 0xC8, 0xFF),
                        Color.FromArgb(0x0E, 0x9F, 0xC8, 0xFF),
                        Color.FromArgb(0x1E, 0x9F, 0xC8, 0xFF)
                    },
                    Positions = new[] { 0f, 0.6f, 1f }
                };
                edge.InterpolationColors = eb;
                g.FillRectangle(edge, new Rectangle(rect.Right - rect.Width / 4, rect.Y, rect.Width / 4, rect.Height));
            }

            // 1c. reflet diagonal oblique traversant le verre
            using (var shine = new GraphicsPath())
            {
                float w = rect.Width, h = rect.Height;
                shine.AddPolygon(new[]
                {
                    new PointF(rect.X + w * 0.06f, rect.Y),
                    new PointF(rect.X + w * 0.30f, rect.Y),
                    new PointF(rect.X + w * 0.12f, rect.Bottom),
                    new PointF(rect.X - w * 0.12f, rect.Bottom)
                });
                using (var sb = new LinearGradientBrush(rect,
                                                        Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF),
                                                        Color.FromArgb(0, 0xFF, 0xFF, 0xFF), LinearGradientMode.Horizontal))
                {
                    var cb = new ColorBlend(3)
                    {
                        Colors = new[]
                        {
                            Color.FromArgb(0, 0xFF, 0xFF, 0xFF),
                            Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF),
                            Color.FromArgb(0, 0xFF, 0xFF, 0xFF)
                        },
                        Positions = new[] { 0f, 0.18f, 1f }
                    };
                    sb.InterpolationColors = cb;
                    g.FillPath(sb, shine);
                }
            }

            // 2. réfraction : le bas du verre capte le fond et le renvoie en bleu froid
            int bandH = Math.Max(10, rect.Height / 3);
            using (var band = new LinearGradientBrush(new Rectangle(rect.X, rect.Bottom - bandH, rect.Width, bandH),
                                                      Color.FromArgb(0, 0x8C, 0xB4, 0xFF),
                                                      Color.FromArgb(0x22, 0x8C, 0xB4, 0xFF), LinearGradientMode.Vertical))
            {
                var blend = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 0x8C, 0xB4, 0xFF),
                        Color.FromArgb(0x14, 0x8C, 0xB4, 0xFF),
                        Color.FromArgb(0x22, 0x8C, 0xB4, 0xFF)
                    },
                    Positions = new[] { 0f, 0.7f, 1f }
                };
                band.InterpolationColors = blend;
                g.FillRectangle(band, new Rectangle(rect.X, rect.Bottom - bandH, rect.Width, bandH));
            }

            // 3. ombre de contact sous l'arête basse
            int contactH = Math.Max(4, rect.Height / 8);
            using (var contact = new LinearGradientBrush(new Rectangle(rect.X, rect.Bottom - contactH, rect.Width, contactH),
                                                         Color.FromArgb(0, 0, 0, 0),
                                                         Color.FromArgb(0x2C, 0x00, 0x00, 0x00), LinearGradientMode.Vertical))
                g.FillRectangle(contact, new Rectangle(rect.X, rect.Bottom - contactH, rect.Width, contactH));

            // 4. rim light : arête claire sur tout le pourtour
            using (var rim = new Pen(new LinearGradientBrush(rect,
                                                             Color.FromArgb(0xB4, 0xFF, 0xFF, 0xFF),
                                                             Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF),
                                                             LinearGradientMode.Vertical), 2.2f))
                g.DrawPath(rim, path);

            // 4b. arête interne sombre : épaisseur du verre
            using (var inner = Ui.RoundRect(new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6), 9))
            using (var innerPen = new Pen(Color.FromArgb(0x20, 0x00, 0x00, 0x00), 1.6f))
                g.DrawPath(innerPen, inner);

            // 5. éclat spéculaire diffus en haut
            using (var spec = new LinearGradientBrush(new Rectangle(rect.X + 10, rect.Y + 2, rect.Width - 20, 10),
                                                      Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF),
                                                      Color.FromArgb(0, 0xFF, 0xFF, 0xFF), LinearGradientMode.Horizontal))
            {
                var sb = new ColorBlend(3)
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(0, 0xFF, 0xFF, 0xFF)
                    },
                    Positions = new[] { 0f, 0.45f, 1f }
                };
                spec.InterpolationColors = sb;
                g.FillRectangle(spec, new Rectangle(rect.X + 10, rect.Y + 2, rect.Width - 20, 10));
            }

            g.Restore(saved);
        }

        /// Surface de la Toolbox : carte Bento pleine, dessinée aussi sous les boutons
        /// (fond transparent) pour que rien ne réapparaisse en rectangle.
        /// Bouton de barre de titre : pastille ronde en verre sombre (dégradé, liseré 1px,
        /// reflet spéculaire), glyphe tracé au trait, halo accentué au survol, teinte rouge pour fermer.
        sealed class CaptionButton : Button
        {
            readonly bool isClose;
            bool hover, down;

            public CaptionButton(bool close)
            {
                isClose = close;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : Color.FromArgb(10, 12, 18));

                int d = Math.Min(Width, Height) - 2;
                var square = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);

                Color top, bottom, edge, glyph;
                if (isClose)
                {
                    top = hover ? Color.FromArgb(0xE8, 0x5A, 0x5A) : Color.FromArgb(0x3A, 0x22, 0x28);
                    bottom = hover ? Color.FromArgb(0xC0, 0x3A, 0x3A) : Color.FromArgb(0x24, 0x16, 0x1C);
                    edge = hover ? Color.FromArgb(0xFF, 0x9E, 0x9E) : Color.FromArgb(0x6A, 0x3A, 0x44);
                    glyph = hover ? Color.White : Color.FromArgb(0xE0, 0xA8, 0xA8);
                }
                else
                {
                    top = hover ? Color.FromArgb(0x34, 0x3C, 0x4E) : Color.FromArgb(0x24, 0x2A, 0x38);
                    bottom = hover ? Color.FromArgb(0x22, 0x28, 0x34) : Color.FromArgb(0x16, 0x1A, 0x24);
                    edge = hover ? Color.FromArgb(0x6E, 0x8A, 0xB4) : Color.FromArgb(0x3E, 0x46, 0x5A);
                    glyph = hover ? Color.White : Color.FromArgb(0xC8, 0xD2, 0xE0);
                }
                if (down) { top = bottom = Color.FromArgb(0x10, 0x12, 0x18); }

                using (var p = Ui.RoundRect(square, 3))
                {
                    using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(square, top, bottom, 90f))
                        g.FillPath(br, p);
                    using (var pen = new Pen(edge, 1f)) g.DrawPath(pen, p);
                }

                // reflet spéculaire : arc clair sur le haut du carré
                var spec = new Rectangle(square.X + 2, square.Y + 2, square.Width - 4, square.Height / 2);
                using (var p = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    p.AddArc(spec, 180f, 180f);
                    using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Rectangle(spec.X, spec.Y, spec.Width, Math.Max(1, spec.Height)),
                        Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                        g.FillPath(br, p);
                }

                // glyphe au trait, centré
                float cx = Width / 2f, cy = Height / 2f, r = d * 0.24f;
                using (var pen = new Pen(glyph, 1.6f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                {
                    if (isClose)
                    {
                        g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                        g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                    }
                    else g.DrawLine(pen, cx - r, cy, cx + r, cy);
                }
            }
        }

        /// Bouton de barre de titre (réduire / fermer) : pastille ronde owner-drawn.
        static Button MakeCaptionButton(bool close, Control parent, Action onClick)
        {
            var b = new CaptionButton(close)
            {
                TabStop = false,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => onClick();
            return b;
        }

        internal void PaintToolboxSurface(Graphics g, Control sys)
        {
            var body = new Rectangle(1, 1, sys.Width - 3, sys.Height - 3);
            using (var path = Ui.RoundRect(body, 14))
            {
                using (var b = new SolidBrush(Ui.DarkCard))
                    g.FillPath(b, path);

                using (var pen = new Pen(Color.FromArgb(0x3A, 0x9F, 0xB4, 0xD0), 1.2f)) g.DrawPath(pen, path);
            }
        }

        /// Dessine la surface de la Toolbox sous un contrôle enfant (coordonnées locales du contrôle) :
        /// les boutons à fond transparent retrouvent la carte au lieu d'un bloc plein.
        internal void PaintToolboxUnder(Graphics g, Control child)
        {
            var sys = ToolboxControl;
            if (sys == null) return;
            var saved = g.Save();
            int ox = 0, oy = 0;
            for (var p = child; p != null && p != sys; p = p.Parent) { ox += p.Left; oy += p.Top; }
            var offset = new Point(ox, oy);
            g.TranslateTransform(-offset.X, -offset.Y);
            PaintToolboxSurface(g, sys);
            g.Restore(saved);
        }

        /// Redessine la bande de fond (image backv2.jpg) correspondant au rectangle demandé (header/footer, coins de carte),
        /// en l'échantillonnant aux coordonnées globales : la bande est alors identique au fond.
        internal void PaintSkyBand(Graphics g, Rectangle band)
        {
            var saved = g.Save();
            // clip sur la bande, en coordonnees de la FENETRE (le clip est applique apres la translation)
            g.TranslateTransform(-band.X, -band.Y);
            using (var frame = Ui.RoundRect(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 18)) g.SetClip(frame, CombineMode.Intersect);
            g.SetClip(band, CombineMode.Intersect);
            var full = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            if (backImage != null) g.DrawImage(backImage, full);
            else using (var b = new SolidBrush(Ui.DarkBg)) g.FillRectangle(b, full);
            g.Restore(saved);
        }

        /// Retire les émojis/symboles en tête des chaînes traduites (GDI+ les affiche «▯»).
        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int i = 0;
            while (i < s.Length && s[i] > '─') i++;
            return (i == 0) ? s : s.Substring(i).TrimStart();
        }

        /// Bento Grid : header sombre 75px + Hero Card + grille 2x2 pleine largeur + Toolbox + footer.
        /// Recrée tout l'agencement (appelé au démarrage et après un changement de langue).
        void BuildLayout()
        {
            SuspendLayout();
            Controls.Clear();
            Font = Ui.Body;   // police choisie dans le menu POLICE : tous les contrôles héritent de la fenêtre

            int CW = ClientSize.Width;

            // ---- header NOIR/BLEU NUIT 75px — le logo redevient visible ; titre dessiné UNE seule
            // fois + badge PRE-RELEASE ; fine ligne dégradée (jaune→orange→rouge) de 2px tout en bas.
            var header = new Panel { Bounds = new Rectangle(0, 0, CW, 100), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            header.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // logo centré en haut, badges alignés à droite
                const int logoH = 100;
                var logo = Ui.Resource("logo_640.png");
                if (logo != null)
                {
                    float logoW = logo.Width * (logoH / (float)logo.Height);
                    g.DrawImage(logo, (header.Width - logoW) / 2f, (header.Height - logoH) / 2f, logoW, logoH);
                }
                // badges néon : halo orange + contour dégradé + texte orange, calés à droite
                using (var bf = Ui.BrandFont(10.5f, FontStyle.Bold))
                {
                    var labels = new[] { Version.Number.ToUpperInvariant(), Lang.Get("Launcher.HeaderSubtitle", "Lanceur") + "  ·  " + Version.AppVersion };
                    var sizes = new int[labels.Length];
                    int total = 0;
                    for (int i = 0; i < labels.Length; i++)
                    {
                        sizes[i] = (int)g.MeasureString(labels[i], bf).Width + 30;
                        total += sizes[i] + (i > 0 ? 14 : 0);
                    }
                    float bx = header.Width - 18 - total - header.Width * 0.10f;   // 5 % a gauche des precedents
                    for (int li = 0; li < labels.Length; li++)
                    {
                        var label = labels[li];
                        var r = new Rectangle((int)bx, 18, sizes[li], 36);
                        using (var path = Ui.RoundRect(r, 18))
                        {
                            // halo néon extérieur
                            for (int i = 5; i >= 1; i--)
                                using (var glow = new Pen(Color.FromArgb(10 + (6 - i) * 5, 0xFF, 0x8A, 0x1E), i * 3.2f))
                                using (var gp = Ui.RoundRect(Rectangle.Inflate(r, i, i), 18 + i))
                                    g.DrawPath(glow, gp);
                            // fond bleu nuit
                            using (var fill = new SolidBrush(Color.FromArgb(204, 0x10, 0x1A, 0x30))) g.FillPath(fill, path);
                            // contour dégradé ambre → orange
                            using (var bp = new LinearGradientBrush(r, Color.FromArgb(0xFF, 0xC7, 0x4A), Color.FromArgb(0xF5, 0x7C, 0x10), LinearGradientMode.Horizontal))
                            using (var pen = new Pen(bp, 1.6f)) g.DrawPath(pen, path);
                        }
                        TextRenderer.DrawText(g, label, bf, r, Color.FromArgb(0xFF, 0x9E, 0x3C), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        bx = r.Right + 14;
                    }
                }
            };

            // fenêtre sans bordure : le header sert de barre de titre (glisser pour déplacer)
            Point dragStart = Point.Empty;
            bool dragging = false;
            header.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; } };
            header.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
            };
            header.MouseUp += (s, e) => dragging = false;

            // boutons Fenêtre : réduire / fermer, discrets, alignés en haut à droite
            var btnMin = MakeCaptionButton(false, header, () => WindowState = FormWindowState.Minimized);
            var btnClose = MakeCaptionButton(true, header, Close);
            btnMin.Bounds = new Rectangle(CW - 108, 14, 24, 24);
            btnClose.Bounds = new Rectangle(CW - 74, 14, 24, 24);
            header.Controls.Add(btnMin);
            header.Controls.Add(btnClose);

            Controls.Add(header);

            // ---- Hero Card « Diffuseur (Streamer) » : la plus haute, pleine largeur, teinte ambre.
            Controls.Add(MakeCard(Tools[0], new Rectangle(24, 102, CW - 48, 100)));

            // ---- Grille 2x2 pleine largeur, 8px d'écart : Statistiques / Paramètres puis Diagnostics / Wizard.
            int colW = (CW - 48 - 8) / 2;
            int xL = 24, xR = 24 + colW + 8;
            const int cardH = 92, gap = 10, gridY = 212;
            Controls.Add(MakeCard(Tools[1], new Rectangle(xL, gridY, colW, cardH)));                          // Statistiques
            Controls.Add(MakeCard(Tools[2], new Rectangle(xR, gridY, colW, cardH)));                          // Paramètres
            Controls.Add(MakeCard(Tools[3], new Rectangle(xL, gridY + cardH + gap, colW, cardH)));            // Diagnostics
            Controls.Add(MakeCard(Tools[4], new Rectangle(xR, gridY + cardH + gap, colW, cardH)));            // Wizard

            // ---- Toolbox OpenXR : statut runtime/preset + boutons d'action (logique métier RefreshSystem inchangée)
            sys.Location = new Point(24, gridY + 2 * cardH + gap + 12);
            sys.Size = new Size(CW - 48, 120);   // ajusté au contenu par RefreshSystem()
            sys.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sys.BackColor = Color.Transparent;   // le cadre arrondi est peint par le parent : les coins laissent voir le ciel
            Controls.Add(sys);

            // ---- footer : Aide / GitHub à droite, langue / BACKGROUND / POLICE à gauche.
            // Boutons plats basiques (rectangle, aucun dessin custom), fond opaque sombre et texte orange.
            footerStatus = null;
            var helpLink = Lang.Get("Launcher.HelpLink", "Aide & GitHub");
            var footerY = ClientSize.Height - 40;
            int rightX = CW - 24;
            var yellow = Color.FromArgb(0xFF, 0xD2, 0x3C);   // Aide / GitHub
            var red = Color.FromArgb(0xE8, 0x3A, 0x3A);      // langue
            var purple = Color.FromArgb(0xA8, 0x5C, 0xFF);   // BACKGROUND
            var blue = Color.FromArgb(0x4F, 0x8F, 0xF0);     // POLICE
            Action<string, Action, Color> addRight = (txt, act, c) =>
            {
                var b = Flat(txt, act, c, c);
                rightX -= b.Width;
                b.Bounds = new Rectangle(rightX, footerY, b.Width, 34);
                b.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                Controls.Add(b);
                rightX -= 10;
            };
            addRight("GitHub", () => Process.Start("https://github.com/AuraFlashXR/AuraFlashXR"), yellow);
            addRight(helpLink.Split('&')[0].Trim(), () => Process.Start("https://discord.gg/5dg72jKdTR"), yellow);   // Aide → Discord

            var langs = Lang.AvailableLanguages();
            int footerX = 24;
            Action<Button> addLeft = b =>
            {
                b.Bounds = new Rectangle(footerX, footerY, b.Width, 34);
                b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                Controls.Add(b);
                footerX = b.Right + 10;
            };
            if (langs.Count > 0)
            {
                int sel = 0;
                for (int i = 0; i < langs.Count; i++)
                    if (string.Equals(langs[i].code, Lang.CurrentCode, StringComparison.OrdinalIgnoreCase)) sel = i;
                var langBtn = Flat(langs[sel].name, null, red, red);
                langBtn.Click += (s, e) =>
                {
                    var menu = new ContextMenuStrip();
                    for (int i = 0; i < langs.Count; i++)
                    {
                        var code = langs[i].code;
                        var item = new ToolStripMenuItem(langs[i].name) { Checked = i == sel };
                        item.Click += (s2, e2) =>
                        {
                            if (string.Equals(code, Lang.CurrentCode, StringComparison.OrdinalIgnoreCase)) return;
                            Lang.Load(code);
                            BuildLayout();
                            RefreshSystem();
                            Invalidate(true);
                        };
                        menu.Items.Add(item);
                    }
                    menu.Show(langBtn, new Point(0, -menu.PreferredSize.Height));
                };
                addLeft(langBtn);
            }
            var bgBtn = Flat(Lang.Get("Launcher.BackgroundLabel", "BACKGROUND"), null, purple, purple);
            bgBtn.Click += (s, e) => ShowBackgroundMenu(bgBtn);
            addLeft(bgBtn);
            var fontBtn = Flat(Lang.Get("Launcher.FontLabel", "POLICE"), null, blue, blue);
            fontBtn.Click += (s, e) => ShowFontMenu(fontBtn);
            addLeft(fontBtn);

            ResumeLayout();
        }

        /// Bouton du footer : coins arrondis (rayon 10 px) peints a la main, le fond de la fenetre passe
        /// derriere les coins (aucun rectangle noir), aucun `Region` (pas de crenelage).
        static Button Flat(string text, Action act) => Flat(text, act, Color.FromArgb(0xFF, 0x8A, 0x1E), Color.FromArgb(0xFF, 0x9E, 0x3C));

        /// Meme bouton, avec une teinte (bordure) et une couleur de texte propres.
        static Button Flat(string text, Action act, Color accent, Color textColor)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = false,
                Size = new Size(ButtonWidth(text), 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = textColor,
                Font = Ui.BrandFont(10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.Transparent;
            b.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool hover = false, down = false;
            b.MouseEnter += (s, e) => { hover = true; b.Invalidate(); };
            b.MouseLeave += (s, e) => { hover = false; down = false; b.Invalidate(); };
            b.MouseDown += (s, e) => { down = true; b.Invalidate(); };
            b.MouseUp += (s, e) => { down = false; b.Invalidate(); };
            b.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                // fond de la fenetre : la bande de ciel aux coordonnees globales du bouton (aucun rectangle)
                if (b.TopLevelControl is LauncherForm host)
                    host.PaintSkyBand(g, b.RectangleToScreen(b.ClientRectangle));
                var r = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                using (var path = Ui.RoundRect(r, 10))
                {
                    var fill = down ? Color.FromArgb(0x24, 0x34, 0x58) : hover ? Color.FromArgb(0x1A, 0x28, 0x46) : Color.FromArgb(0x10, 0x1A, 0x30);
                    using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                    using (var pen = new Pen(accent, 1.6f)) g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, b.Text, b.Font, b.ClientRectangle, b.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            };
            if (act != null) b.Click += (s, e) => act();
            return b;
        }

        /// Footer button: pastille ambre (texte orange lisible) pour Aide / GitHub.
        static Button Link(string text, Action act)
        {
            var amber = Color.FromArgb(0xFF, 0x8A, 0x1E);
            var b = new ModernButton { Text = text, AutoSize = true, MinimumSize = new Size(72, 26), Margin = new Padding(0, 0, 8, 0), AccentTint = amber };
            b.ForeColor = Color.FromArgb(0xFF, 0xB3, 0x5C);
            b.Click += (s, e) => act();
            b.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
                using (var path = Ui.RoundRect(r, r.Height / 2))
                using (var pen = new Pen(Color.FromArgb(0xCC, amber), 1.3f))
                    g.DrawPath(pen, path);
            };
            return b;
        }

        Control MakeCard(Tool t, Rectangle bounds)
        {
            var exe = Path.Combine(Paths.Dir, t.Exe);
            var title = Clean(Lang.Get("Launcher.Tool." + t.Key + ".Title", t.Title));
            var what = Clean(Lang.Get("Launcher.Tool." + t.Key + ".Desc", t.What));

            var card = new Card
            {
                Bounds = bounds,
                IsPrimary = t.Primary,
                // teinte par outil : ambre (diffuseur), or (stats), bleu acier (paramètres), orange (diagnostics), bleu (casque)
                Accent = t.Key == "Stats" ? Ui.Gold
                       : t.Key == "Settings" ? Color.FromArgb(0x5B, 0x9B, 0xE8)
                       : t.Key == "Diagnostics" ? Color.FromArgb(0xFF, 0x9A, 0x3C)
                       : t.Key == "Wizard" ? Color.FromArgb(0x4F, 0x8F, 0xF0)
                       : Ui.AccentLight,
                IconKey = t.Key,
                TitleText = title,
                DescText = what,
                Enabled = demoReady || File.Exists(exe),
                AccessibleName = title + ". " + what,
                AccessibleRole = AccessibleRole.PushButton
            };
            card.Chosen = () => Launch(t, exe);
            return card;
        }

        // What needs attention, and only that.
        void RefreshSystem()
        {
            sys.SuspendLayout();
            sys.Controls.Clear();
            var lines = new System.Collections.Generic.List<Control>();
            bool ours = demoReady || OpenXr.IsOurs();
            var curRt = OpenXr.ActiveRuntime();
            bool isSteam = curRt.Contains("steamxr");
            string curName = ours ? "AuraFlashXR (OpenXR direct)" : (isSteam ? "SteamVR (Compatible BattlEye)" : (curRt.Length > 0 ? OpenXr.Name(curRt) : "Aucun"));

            var rtButtons = new System.Collections.Generic.List<(string, Action)>();
            if (File.Exists(Paths.Manifest))
                rtButtons.Add((ours ? Lang.Get("Launcher.Btn.AuraFlashXrActive") : Lang.Get("Launcher.Btn.ActivateAuraFlashXr"), (Action)UseAuraFlashXr));
            rtButtons.Add((isSteam ? Lang.Get("Launcher.Btn.SteamVrActive") : Lang.Get("Launcher.Btn.ActivateSteamVr"), (Action)UseSteamVr));

            // Bouton Hôte : Démarré/Arrêté (ON/OFF) identique au Streamer.exe
            bool hostRunning = Process.GetProcessesByName("auraflashxr_host").Length > 0;
            rtButtons.Add((hostRunning ? Lang.Get("Launcher.Btn.HostActive") : Lang.Get("Launcher.Btn.HostInactive"), (Action)ToggleHostFromLauncher));

            // Bouton Lancer SteamVR identique au Streamer.exe
            bool vrRunning = Process.GetProcessesByName("vrserver").Length > 0;
            rtButtons.Add((vrRunning ? Lang.Get("Launcher.Btn.SteamVrRunning") : Lang.Get("Launcher.Btn.LaunchSteamVr"), (Action)LaunchSteamVrRuntime));
            rtButtons.Add((IsAdbRunning() ? Lang.Get("Launcher.Btn.DisableAdb") : Lang.Get("Launcher.Btn.EnableAdb"), (Action)ToggleAdb));
            rtButtons.Add((Lang.Get("Launcher.Btn.InstallQuestApk"), (Action)InstallQuestApk));

            string statusMsg = ours ? Lang.Get("Launcher.Status.AuraFlashXrActive")
                : (isSteam ? Lang.Get("Launcher.Status.SteamVrActive")
                : Lang.Get("Launcher.Status.OtherRuntime", curName));

            // Bouton 'CHARGER LE PRESET' + bulle config - sur la MEME ligne que les boutons runtime
            // (aligne avec le bouton AuraFlashXR du haut, la bulle a droite du bouton Charger le Preset)
            var presets = Preset.List();
            var presetNames = presets;
            var displayItems = new System.Collections.Generic.List<string>();
            foreach (var p in presets)
            {
                string pUpper = p.ToUpperInvariant();
                string codecLabel;
                string gpuLabel;
                bool hasGpu = true;
                var codecFamily = Preset.CodecFamilyOf(p);
                if (codecFamily != null) { codecLabel = codecFamily; gpuLabel = ""; hasGpu = false; }
                else if (pUpper.Contains("LOW")) { codecLabel = Lang.Get("Launcher.Preset.Codec.H264"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.Low"); }
                else if (pUpper.Contains("MID")) { codecLabel = Lang.Get("Launcher.Preset.Codec.Hevc8"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.Mid"); }
                else if (pUpper.Contains("OPTI")) { codecLabel = Lang.Get("Launcher.Preset.Codec.Hevc10Split"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.High"); }
                else if (pUpper.Contains("HIGH")) { codecLabel = Lang.Get("Launcher.Preset.Codec.Hevc10Dual"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.High"); }
                else { codecLabel = Lang.Get("Launcher.Preset.Tooltip.Custom"); gpuLabel = ""; hasGpu = false; }
                if (hasGpu)
                    displayItems.Add(p + "  ·  " + codecLabel + "  ·  " + Lang.Get("Launcher.Preset.GpuMinPrefix") + gpuLabel);
                else
                    displayItems.Add(p + "  ·  " + codecLabel);
            }
            // Popup menu for preset selection (dark styled ContextMenuStrip)
            var presetMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                BackColor = Color.FromArgb(0x14, 0x1C, 0x2B),
                ForeColor = Ui.DarkText,
                Font = Ui.SemiBold,
                DropShadowEnabled = true,
                Renderer = new Ui.DarkMenuRenderer()   // inherited by the per-encoder submenus (same dark look)
            };
            int curPreset = presets.IndexOf(Preset.IsActiveDefault ? Preset.DefaultName : Preset.Active);

            // Applies a preset picked in the menu (same behaviour as before, shared by the flat items and the per-encoder submenus).
            Action<int> applyPreset = (idx) =>
            {
                var chosen = (idx >= 0 && idx < presetNames.Count) ? presetNames[idx] : "";
                if (chosen.Equals(Preset.IsActiveDefault ? Preset.DefaultName : Preset.Active, StringComparison.OrdinalIgnoreCase)) return;
                bool hostRunning = Process.GetProcessesByName("auraflashxr_host").Length > 0;
                bool gameRunning = Process.GetProcessesByName("vrserver").Length > 0;
                if (hostRunning && gameRunning && MessageBox.Show(this, Lang.Get("Launcher.Preset.SwitchGameRunning"), Lang.Get("App.Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                Preset.Active = chosen;
                Log.Write("INFO", "preset switched to '" + chosen + "' via Launcher toolbox");
                UpdateConfigBubble();
                if (hostRunning)
                {
                    foreach (var hp in Process.GetProcessesByName("auraflashxr_host")) StopHostGracefully(hp);
                    // same guard as the Streamer - never restart the host while SteamVR owns UDP 9943/9944.
                    if (Process.GetProcessesByName("vrserver").Length == 0)
                    {
                        var hostExe = Path.Combine(Paths.Bin, "auraflashxr_host.exe");
                        if (!File.Exists(hostExe)) hostExe = Paths.Host;   // same fallback as the Streamer (auraflashxr_host.exe)
                        if (File.Exists(hostExe))
                        {
                            Preset.SyncPairing(Preset.Active);
                            var psi = new ProcessStartInfo(hostExe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Paths.Dir, Arguments = $"--install-dir \"{Preset.HostInstallDir}\" --shim --daemon --gui" };
                            Process.Start(psi);
                            Log.Write("INFO", "auraflashxr_host.exe restarted with preset '" + chosen + "'");
                        }
                    }
                    else Log.Write("INFO", "auraflashxr_host.exe not restarted after preset switch: SteamVR (vrserver.exe) is running");
                }
            };

            // One preset per encoder (OPTI H.264 / OPTI HEVC / OPTI AV1): every preset is listed flat, under its own name.
            for (int mi = 0; mi < displayItems.Count; mi++)
            {
                var capturedIdx = mi;
                var presetName = presetNames[mi];
                var menuItem = new ToolStripMenuItem(displayItems[mi])
                {
                    AutoSize = true,
                    Padding = new Padding(14, 6, 18, 6),
                    ForeColor = Ui.DarkText,
                    Font = Ui.SemiBold,
                    Checked = (mi == curPreset),
                    CheckOnClick = false,
                    BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                };
                menuItem.Click += (s, e) => applyPreset(capturedIdx);
                presetMenu.Items.Add(menuItem);
            }

            // section suppression - separateur + header + presets supprimables
            presetMenu.Items.Add(new ToolStripSeparator { BackColor = Color.FromArgb(0x14, 0x1C, 0x2B) });
            var delHeader = new ToolStripMenuItem(Lang.Get("Preset.Menu.DeleteHeader"))
            {
                Enabled = false,
                ForeColor = Color.FromArgb(120, 140, 160),
                Font = Ui.Body,
                BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
            };
            presetMenu.Items.Add(delHeader);

            int delCount = 0;
            foreach (var dp in presets)
            {
                if (!Preset.Deletable(dp)) continue;
                delCount++;
                var capturedDel = dp;
                var delItem = new ToolStripMenuItem("✕ " + dp)
                {
                    AutoSize = true,
                    Padding = new Padding(14, 6, 18, 6),
                    ForeColor = Color.FromArgb(255, 130, 130),
                    Font = Ui.SemiBold,
                    BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                };
                delItem.Click += (s, e) =>
                {
                    var confirmMsg = string.Format(Lang.Get("Preset.Menu.DeleteConfirm"), capturedDel);
                    if (MessageBox.Show(this, confirmMsg, Lang.Get("App.Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                    var err = Preset.Delete(capturedDel);
                    if (err == null)
                    {
                        MessageBox.Show(this, string.Format(Lang.Get("Preset.Menu.DeleteSuccess"), capturedDel), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Log.Write("INFO", "preset deleted via Launcher menu: " + capturedDel);
                        try { RefreshSystem(); } catch { }
                    }
                    else if (err == "reserved")
                    {
                        MessageBox.Show(this, Lang.Get("Preset.Menu.DeleteReserved"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(this, Lang.Get("Preset.Menu.DeleteError") + " " + err, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                presetMenu.Items.Add(delItem);
            }
            if (delCount == 0)
            {
                var noDel = new ToolStripMenuItem(Lang.Get("Preset.Menu.NoPresets"))
                {
                    Enabled = false,
                    ForeColor = Color.FromArgb(120, 140, 160),
                    Font = Ui.Body,
                    BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                };
                presetMenu.Items.Add(noDel);
            }

            var loadPresetBtn = new ModernButton { Text = Lang.Get("Launcher.Preset.LoadButton"), Margin = new Padding(8, 2, 0, 2), MinimumSize = new Size(180, 28) };
            loadPresetBtn.Click += (s, e) =>
            {
                if (presetMenu.Items.Count > 0)
                    presetMenu.Show(loadPresetBtn, new Point(0, loadPresetBtn.Height + 2));
            };
            UpdateConfigBubble();
            
            Action openPresetMenu = null;
            var runtimeButtons = rtButtons.Take(4).ToArray();
            var toolButtons = rtButtons.Skip(4).Concat(new[] { (Lang.Get("Launcher.Preset.LoadButton"), (Action)(() => openPresetMenu?.Invoke())) }).ToArray();
            // menus BACKGROUND et POLICE : dans le footer, à la suite du sélecteur de langue
            var mainRow = Row("", "", Color.SeaGreen, runtimeButtons);
            var secondRow = Row("", "", Color.SeaGreen, toolButtons);
            openPresetMenu = () => { if (presetMenu.Items.Count > 0) presetMenu.Show(mainRow, new Point(mainRow.Width - 200, mainRow.Height + 2)); };
            lines.Add(mainRow);
            lines.Add(secondRow);

            var presetRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 4), MaximumSize = new Size(1400, 0), Tag = "preset-block" };
            presetRow.Controls.Add(loadPresetBtn);
            presetRow.Controls.Add(configBubble);
            loadPresetBtn.Visible = false;
            presetRow.Visible = true;
            lines.Add(presetRow);
            var risky = demoReady ? new System.Collections.Generic.List<XrLayer>() : XrLayers.ActiveImplicit().Where(l => l.MayCaptureInput).ToList();
            if (risky.Count > 0)
                lines.Add(Row("⚠", risky.Count == 1 ? Lang.Get("Launcher.Alert.OpenXrLayers") : Lang.Get("Launcher.Alert.OpenXrLayersMulti", risky.Count), Color.Firebrick, new[] { (Lang.Get("Launcher.Btn.Check"), (Action)(() => { using (var f = new LayersForm()) f.ShowDialog(this); RefreshSystem(); })) }));
            if (firewall != null && (firewall.State == FirewallState.Missing || firewall.State == FirewallState.Blocked))
                lines.Add(Row("⚠", firewall.State == FirewallState.Blocked ? Lang.Get("Launcher.Alert.FirewallBlocked") : Lang.Get("Launcher.Alert.FirewallMissing"), Color.Firebrick, new[] { (Lang.Get("Launcher.Btn.Fix"), (Action)(() => { using (var f = new FirewallForm()) f.ShowDialog(this); firewall = null; Shown_Recheck(); })) }));
            bool allGood = (ours || isSteam) && lines.Count == 3;

            if (footerStatus != null)
            {
                footerStatus.Text = "● " + (allGood ? Lang.Get("Launcher.StatusReady", "Prêt") : Lang.Get("Launcher.StatusAttention", "Attention"));
                footerStatus.ForeColor = allGood ? Ui.Emerald : Color.Firebrick;
            }

            // Niveau 1 : en-tête du panneau — titre à gauche, état + pill de configuration à droite.
            var headerRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 10), BackColor = Color.Transparent };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            
            var chip = new Label { Text = "", AutoSize = false, Size = new Size(26, 26), Margin = new Padding(0, 2, 10, 0), BackColor = Color.Transparent };
            chip.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Icône clé à molette pleine, sans cadre : tête ouverte en haut à gauche, fourche en bas à droite.
                Icons.DrawWrench(g, new RectangleF(1, 1, 24, 24), Ui.AccentLight);
            };
            headerRow.Controls.Add(chip, 0, 0);
            var titleState = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            titleState.Controls.Add(new Label { Text = Lang.Get("Launcher.ToolboxHeader"), AutoSize = true, Font = Ui.BrandFont(13f, FontStyle.Bold), ForeColor = Ui.DarkText, Margin = new Padding(0, 3, 16, 0), BackColor = Color.Transparent });
            titleState.Controls.Add(new Label { Text = (allGood ? "● " : "▲ ") + (allGood ? Lang.Get("Launcher.StatusReady") : Lang.Get("Launcher.StatusAttention")), AutoSize = true, Font = Ui.Bold, ForeColor = allGood ? Ui.Emerald : Color.Firebrick, Margin = new Padding(0, 7, 10, 0), BackColor = Color.Transparent });
            titleState.Controls.Add(new Label { Text = statusMsg, AutoSize = true, Font = Ui.Body, ForeColor = Ui.DarkMuted, Margin = new Padding(0, 7, 0, 0), BackColor = Color.Transparent });
            headerRow.Controls.Add(titleState, 1, 0);
            configBubble.Margin = new Padding(12, 3, 0, 0);
            // bulle de configuration : pastille (halo + fond plat + contour), comme les badges PRE-RELEASE
            configBubble.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var lbl = (Label)s;
                var rect = new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1);
                var accent = configBubbleReadOnly ? Color.FromArgb(0xFF, 0xC8, 0x64) : Color.FromArgb(0x4A, 0xE3, 0xA6);
                // pastille pleine hauteur : rayon = moitié de la hauteur, halo interne (jamais hors du contrôle)
                int radius = rect.Height / 2;
                using (var path = Ui.RoundRect(rect, radius))
                {
                    using (var fill = new SolidBrush(Color.FromArgb(0x18, accent))) g.FillPath(fill, path);
                    using (var pen = new Pen(Color.FromArgb(0xC0, accent), 1.4f)) g.DrawPath(pen, path);
                }
                using (var inner = Ui.RoundRect(Rectangle.Inflate(rect, -3, -3), Math.Max(1, radius - 3)))
                using (var glow = new Pen(Color.FromArgb(0x28, accent), 1.2f)) g.DrawPath(glow, inner);
            };
            headerRow.Controls.Add(configBubble, 2, 0);
            sys.Controls.Add(headerRow);
            sys.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Ui.DarkBorder, Margin = new Padding(0, 0, 0, 12) });

            // Niveaux 2 et 3 : boutons sur deux lignes épurées (sans labels de catégories sur le côté)
            var groups = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), BackColor = Color.Transparent };
            groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            groups.Controls.Add(ToolboxGroup(Lang.Get("Launcher.ToolboxGroupRuntime", "RUNTIME / SERVEUR"), mainRow), 0, 0);
            groups.Controls.Add(ToolboxGroup(Lang.Get("Launcher.ToolboxGroupTools", "OUTILS DÉPLOIEMENT"), secondRow), 0, 1);
            sys.Controls.Add(groups);

            presetRow.Visible = false;

            foreach (var l in lines.Skip(2)) sys.Controls.Add(l);
            sys.Height = (int)((sys.GetPreferredSize(new Size(sys.Width, 0)).Height + sys.Padding.Vertical) * 0.9);   // marge basse -10 %
            sys.ResumeLayout();
        }

        /// A toolbox group: uppercase caption above its row of buttons.
        static Control ToolboxGroup(string caption, Control buttons)
        {
            var box = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 12) };
            box.Controls.Add(new Label { Text = caption, AutoSize = true, Font = Ui.BrandFont(8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0x8A, 0x99, 0xAE), Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent });
            buttons.Margin = new Padding(0);
            box.Controls.Add(buttons);
            return box;
        }

        void CenterRows()
        {
            if (sys.Width <= 0) return;
            foreach (Control row in sys.Controls)
            {
                int preferred = row.GetPreferredSize(Size.Empty).Width + row.Margin.Horizontal;
                int left = Math.Max(0, (sys.Width - sys.Padding.Horizontal - preferred) / 2);
                row.Margin = new Padding(left, row.Margin.Top, row.Margin.Right, row.Margin.Bottom);
            }
        }

        void Shown_Recheck() { System.Threading.Tasks.Task.Run(() => Firewall.Analyze(Paths.Host)).ContinueWith(t => { try { BeginInvoke((Action)(() => { firewall = t.Result; RefreshSystem(); })); } catch { } }); }

        // ---- menu BACKGROUND : fonds embarqués (bg\*) + dossier docs\images\launcher du package ----
        static readonly string[] BackgroundExts = { ".jpg", ".jpeg", ".png", ".bmp" };
        static string BackgroundDir => Path.Combine(Paths.Docs, "images", "launcher");

        /// Fond choisi : "" = embarqué (back.png), sinon l'image du dossier du package.
        static Image LoadBackground(string choice)
        {
            try
            {
                if (!string.IsNullOrEmpty(choice))
                {
                    var p = Path.Combine(BackgroundDir, choice);
                    if (File.Exists(p)) return Image.FromFile(p);
                }
            }
            catch { }
            return Ui.Resource("back.png");
        }

        void SetBackground(string choice)
        {
            try
            {
                var old = backImage;
                backImage = LoadBackground(choice);
                if (old != null && !ReferenceEquals(old, backImage)) old.Dispose();
                var st = new AppSettings(); st.Background = choice; st.Save();
            }
            catch { }
            Invalidate(true);
        }

        void ShowBackgroundMenu(Control anchor)
        {
            if (anchor == null) return;
            var menu = new ContextMenuStrip();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = new AppSettings().Background ?? "";
            Action<string, string> add = (label, file) =>
            {
                var item = new ToolStripMenuItem(label) { Checked = string.Equals(current, file, StringComparison.OrdinalIgnoreCase) };
                var captured = file;
                item.Click += (s, e) => SetBackground(captured);
                menu.Items.Add(item);
            };
            add(Lang.Get("Launcher.Background.Default", "Fond par défaut"), "");
            // fonds embarqués dans le launcher (ressources bg\<nom>)
            try
            {
                foreach (var n in typeof(LauncherForm).Assembly.GetManifestResourceNames())
                {
                    if (!n.StartsWith("bg" + (char)92, StringComparison.OrdinalIgnoreCase)) continue;
                    var file = n.Substring(3);
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!BackgroundExts.Contains(ext)) continue;
                    var label = Path.GetFileNameWithoutExtension(file);
                    seen.Add(label);
                    add(label, file);
                }
            }
            catch { }
            // fonds déposés dans docs\images\launcher (relu à chaque ouverture du menu)
            try
            {
                if (Directory.Exists(BackgroundDir))
                    foreach (var f in Directory.GetFiles(BackgroundDir).Where(f => BackgroundExts.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f))
                    {
                        var label = Path.GetFileNameWithoutExtension(f);
                        if (!seen.Add(label)) continue;
                        add(label, Path.GetFileName(f));
                    }
            }
            catch { }
            menu.Items.Add(new ToolStripSeparator());
            var open = new ToolStripMenuItem(Lang.Get("Launcher.Background.OpenFolder", "Dossier des fonds…"));
            open.Click += (s, e) => { try { Directory.CreateDirectory(BackgroundDir); Process.Start("explorer.exe", BackgroundDir); } catch { } };
            menu.Items.Add(open);
            menu.Show(anchor, new Point(0, anchor.Height + 2));
        }

        // ---- menu POLICE : familles embarquées + sous-menu de graisse ----
        static string WeightLabel(string weight)
        {
            switch (weight)
            {
                case "regular": return Lang.Get("Launcher.Font.Weight.Regular", "Normal");
                case "semibold": return Lang.Get("Launcher.Font.Weight.SemiBold", "Semi-gras");
                case "extrabold": return Lang.Get("Launcher.Font.Weight.ExtraBold", "Extra-gras");
                default: return Lang.Get("Launcher.Font.Weight.Bold", "Gras");
            }
        }

        void ShowFontMenu(Control anchor)
        {
            if (anchor == null) return;
            var st = new AppSettings();
            string curFam = string.IsNullOrEmpty(st.FontFamily) ? "Orbitron" : st.FontFamily;
            string curW = string.IsNullOrEmpty(st.FontWeight) ? "bold" : st.FontWeight;
            // menu systeme clair : l'apercu de chaque police reste lisible (chaque entree est rendue dans sa police)
            var menu = new ContextMenuStrip { Renderer = new Ui.FontPreviewRenderer() };
            foreach (var fam in Ui.FontFamilies)
            {
                var family = fam;
                var sub = new ToolStripMenuItem(family) { Checked = string.Equals(curFam, family, StringComparison.OrdinalIgnoreCase) };
                // l'apercu des familles est applique par le renderer (le menu systeme ignore Font sur les items de premier niveau)
                sub.Tag = family;
                foreach (var w in Ui.WeightKeys)
                {
                    var weight = w;
                    var item = new ToolStripMenuItem(WeightLabel(weight)) { Checked = string.Equals(curW, weight, StringComparison.OrdinalIgnoreCase) };
                    item.Font = Ui.PreviewFont(family, weight, 10f);
                    item.Click += (s, e) => ApplyFontChoice(family, weight);
                    sub.DropDownItems.Add(item);
                }
                menu.Items.Add(sub);
            }
            menu.Show(anchor, new Point(0, anchor.Height + 2));
        }

        /// Applique la police + graisse choisies, les mémorise et reconstruit la fenêtre.
        void ApplyFontChoice(string family, string weight)
        {
            Ui.SetFontChoice(family, weight);
            try { var st = new AppSettings(); st.FontFamily = family; st.FontWeight = weight; st.Save(); } catch { }
            BuildLayout();
            RefreshSystem();
            Invalidate(true);
        }

        Control Row(string icon, string text, Color c, (string label, Action act)[] buttons)
        {
            var row = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = Math.Max(1, buttons.Length), RowCount = 1, Margin = new Padding(0), BackColor = Color.Transparent };
            if (!string.IsNullOrEmpty(icon) || !string.IsNullOrEmpty(text))
            {
                row.ColumnCount = Math.Max(1, buttons.Length) + 1;
                var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = Color.Transparent };
                if (!string.IsNullOrEmpty(icon)) head.Controls.Add(new Label { Text = icon, AutoSize = true, ForeColor = c, Font = Ui.Bold, Margin = new Padding(0, 7, 6, 0) });
                if (!string.IsNullOrEmpty(text)) head.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = c, Font = Ui.SemiBold, Margin = new Padding(0, 7, 12, 0), MaximumSize = new Size(480, 0) });
                row.Controls.Add(head, 0, 0);
            }
            int first = row.ColumnCount - buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                bool activeGreen = b.label == Lang.Get("Launcher.Btn.SteamVrActive") || b.label == Lang.Get("Launcher.Btn.SteamVrRunning") || b.label == Lang.Get("Launcher.Btn.HostActive") || b.label.Contains(Lang.Get("Launcher.Btn.DisableAdb"));
                bool activeBlue = b.label == Lang.Get("Launcher.Btn.AuraFlashXrActive");
                var bt = new ModernButton { Text = b.label, AutoSize = false, Size = new Size(ButtonWidth(b.label), 34), Margin = new Padding(0, 0, 10, 0) };
                if (activeGreen) { bt.ActiveState = true; bt.AccentTint = Ui.Emerald; }
                else if (activeBlue) { bt.ActiveState = true; bt.AccentTint = Ui.AccentLight; }
                var act = b.act;
                bt.Click += (s, e) => act();
                row.Controls.Add(bt, first + i, 0);
            }
            return row;
        }

        static int ButtonWidth(string label)
        {
            var sz = TextRenderer.MeasureText(label, Ui.SemiBold);
            return Math.Max(104, sz.Width + 40);
        }

        static bool IsAdbRunning() => Process.GetProcessesByName("ADBForwarder").Length > 0;

        static bool IsBuiltInProfile(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Preset.IsBuiltInPreset(name)
                || name.Equals("DEFAULT (USINE)", StringComparison.OrdinalIgnoreCase);
        }

        void UpdateConfigBubble()
        {
            string activeName = Preset.IsActiveDefault ? Preset.DefaultName : Preset.Active;
            bool readOnly = false;
            configBubble.Text = string.Format(Lang.Get("Config.Bubble"), activeName)
                + (readOnly ? "  •  " + Lang.Get("Config.ReadOnly") : "");
            configBubbleReadOnly = readOnly;
            configBubble.BackColor = Color.FromArgb(0x10, 0x1A, 0x20);
            configBubble.ForeColor = readOnly ? Color.FromArgb(0xFF, 0xD8, 0x8C) : Color.FromArgb(0x8C, 0xF0, 0xC8);
            configBubble.Invalidate();
        }

        void ToggleAdb()
        {
            if (IsAdbRunning())
            {
                foreach (var p in Process.GetProcessesByName("ADBForwarder")) { try { p.Kill(); } catch { } }
            }
            else
            {
                string adbExe = Paths.AdbForwarder;
                if (File.Exists(adbExe))
                {
                    try
                    {
                        var psi = new ProcessStartInfo(adbExe) { WorkingDirectory = Path.GetDirectoryName(adbExe), UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized };
                        Process.Start(psi);
                    }
                    catch (Exception ex) { MessageBox.Show(this, "Erreur ADBForwarder : " + ex.Message, "AuraFlashXR par Pre-Release"); }
                }
                else
                {
                    MessageBox.Show(this, "ADBForwarder est introuvable : " + adbExe, "AuraFlashXR par Pre-Release", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            RefreshSystem();
        }

        void InstallQuestApk()
        {
            string scriptPath = Path.Combine(Paths.Dir, "tools", "install", "install_apk_quest.bat");
            if (!File.Exists(scriptPath))
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "install", "install_apk_quest.bat");
            if (!File.Exists(scriptPath))
                scriptPath = Path.Combine(Paths.Dir, "..", "tools", "install", "install_apk_quest.bat");

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Script introuvable :\n" + scriptPath, "AuraFlashXR par Pre-Release", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossible de lancer l'installation de l'APK :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void UseSteamVr()
        {
            if (!OpenXr.SetSteamVr()) OpenXr.Unregister();
            RefreshSystem();
        }

        void LaunchSteamVrRuntime()
        {
            try
            {
                string steamVr = OpenXr.FindSteamVrExecutable();
                if (!string.IsNullOrEmpty(steamVr) && File.Exists(steamVr))
                {
                    string configDir = Preset.IsActiveDefault ? Paths.Config : Path.Combine(Preset.ActiveDir, "config");
                    var psi = new ProcessStartInfo(steamVr) { UseShellExecute = false };
                    psi.EnvironmentVariables["AURAFLASHXR_CONFIG_DIR"] = configDir;
                    Process.Start(psi);
                    Log.Write("INFO", "SteamVR launched via Launcher toolbox, config " + configDir);
                }
                else
                {
                    MessageBox.Show(this, Lang.Get("Launcher.SteamVrNotFound"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.Get("Launcher.CannotLaunchSteamVr") + ex.Message, Lang.Get("Launcher.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ToggleHostFromLauncher()
        {
            var hostProcs = Process.GetProcessesByName("auraflashxr_host");
            if (hostProcs.Length > 0)
            {
                foreach (var hp in hostProcs) StopHostGracefully(hp);
                Log.Write("INFO", "auraflashxr_host.exe stopped via Launcher toolbox");
            }
            else
            {
                if (Process.GetProcessesByName("vrserver").Length > 0)
                {
                    Log.Write("INFO", "auraflashxr_host.exe not started from Launcher: SteamVR (vrserver.exe) is running");
                    return;
                }
                try
                {
                    string hostExe = Path.Combine(Paths.Dir, "bin", "auraflashxr_host.exe");
                    if (!File.Exists(hostExe)) hostExe = Paths.Host;
                    if (File.Exists(hostExe))
                    {
                        if (!Preset.IsActiveDefault) Preset.SyncPairing(Preset.Active);
                        var psi = new ProcessStartInfo(hostExe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Paths.Dir, Arguments = $"--install-dir \"{Preset.HostInstallDir}\" --shim --daemon --gui" };
                        Process.Start(psi);
                        Log.Write("INFO", "auraflashxr_host.exe started via Launcher toolbox (preset: " + (Preset.IsActiveDefault ? "MON PRESET" : Preset.Active) + ")");
                    }
                    else
                    {
                        MessageBox.Show(this, Lang.Get("Launcher.HostNotFound"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Lang.Get("Launcher.CannotStartHost") + ex.Message, Lang.Get("Launcher.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            RefreshSystem();
        }

        static void StopHostGracefully(Process hp) => HostProcess.StopGracefully(hp);

        void UseAuraFlashXr()
        {
            if (!OpenXr.Register(this)) MessageBox.Show(this, Lang.Get("Launcher.ChangeNotApplied"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshSystem();
        }

        void SwitchBack()
        {
            var back = XrLayers.DisabledByUs();
            OpenXr.Unregister();
            if (back.Count > 0 && MessageBox.Show(this, Lang.Get("Launcher.ReenableLayers") + string.Join(", ", back.Select(l => l.Name)) + ") ?", Lang.Get("App.Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                XrLayers.Set(back, true);
            RefreshSystem();
        }

        void Launch(Tool t, string exe, string extraArgs = "")
        {
            if (t.StopsStreamer && Process.GetProcessesByName("streamer").Length > 0)
            {
                if (MessageBox.Show(this, Lang.Get("Launcher.StreamerOpenSamePort"), Lang.Get("App.Title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                foreach (var p in Process.GetProcessesByName("streamer")) { try { p.CloseMainWindow(); p.WaitForExit(10000); } catch { } }
                if (Process.GetProcessesByName("streamer").Length > 0) { MessageBox.Show(this, Lang.Get("Launcher.StreamerNotClosed"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            }
            try
            {
                launched = Process.Start(exe, (t.Args + " " + extraArgs).Trim());
                Log.Write("INFO", "launcher: started " + t.Exe);
            }
            catch (Exception ex) { MessageBox.Show(this, Lang.Get("Launcher.CannotStart") + Path.GetFileName(t.Exe) + " : " + ex.Message, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            FlowLog("hide: launched " + t.Key);
            Hide();
            watch.Tick -= WatchTick; watch.Tick += WatchTick; watch.Start();
        }

        void WatchTick(object s, EventArgs e)
        {
            if ((launched != null && !launched.HasExited) || AnyToolOpen()) return;
            watch.Stop();
            RefreshSystem();
            Show(); Activate();
            FlowLog("show: every tool closed");
            if (testFlow) { var c = new Timer { Interval = 2500 }; c.Tick += (s2, e2) => { FlowLog("sample"); Close(); }; c.Start(); }
        }

        static bool AnyToolOpen() => ToolProcesses.Any(n => Process.GetProcessesByName(n).Length > 0);

        void FlowLog(string what)
        {
            if (string.IsNullOrEmpty(FlowLogPath)) return;
            try { File.AppendAllText(FlowLogPath, $"{DateTime.Now:HH:mm:ss.fff} visible={(Visible ? 1 : 0)} tools={ToolProcesses.Sum(n => Process.GetProcessesByName(n).Length)} {what}\r\n"); } catch { }
        }

        public void ShowAgain() { Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); BringToFront(); }

        public void StartTestFlowPublic(string title) => StartTestFlow(title);
        void StartTestFlow(string title)
        {
            testFlow = true;
            var t = Tools.First(x => string.Equals(Lang.Get("Launcher.Tool." + x.Key + ".Title", x.Title), title, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Key, title, StringComparison.OrdinalIgnoreCase));
            var sampler = new Timer { Interval = 400 }; sampler.Tick += (s, e) => FlowLog("sample"); sampler.Start();
            var go = new Timer { Interval = 1500 };
            go.Tick += (s, e) =>
            {
                go.Stop();
                FlowLog("before launch");
                var shot = Path.Combine(Path.GetTempPath(), "auraflashxr_flow_" + title.Replace(' ', '_') + ".png");
                Launch(t, Path.Combine(Paths.Dir, t.Exe), "--screenshot \"" + shot + "\"");
            };
            go.Start();
            var end = new Timer { Interval = 45000 }; end.Tick += (s, e) => { FlowLog("end"); Close(); }; end.Start();
        }

        [STAThread]
        static void Main()
        {
            Log.Component = "launcher";
            bool first;
            var one = new System.Threading.Mutex(true, "AuraFlashXR_Launcher", out first);
            var showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "AuraFlashXR_Launcher_Show");
            if (!first) { showEvent.Set(); Log.Write("INFO", "launcher: already running, asked it to show itself"); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var args = Environment.GetCommandLineArgs();
            var f = new LauncherForm();
            var waiter = new System.Threading.Thread(() => { while (showEvent.WaitOne()) { try { f.BeginInvoke((Action)f.ShowAgain); } catch { return; } } }) { IsBackground = true };
            waiter.Start();
            var i = Array.IndexOf(args, "--test-flow");
            if (i >= 0 && i + 1 < args.Length) f.Shown += (s, e) => f.StartTestFlowPublic(args[i + 1]);
            try { Application.Run(f); }
            catch (Exception e) { Log.Write("ERROR", "launcher failed: " + e); throw; }
        }
    }
}