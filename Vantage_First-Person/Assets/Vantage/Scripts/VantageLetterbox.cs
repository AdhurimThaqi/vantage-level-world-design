using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The cutscene overlay shared by the intro and the boss reveal: black letterbox bars and film-style subtitles
    /// inside the bottom bar (a small amber label over a white line), so they never cover the picture. A plain
    /// class, not a component: the cutscene owns it and calls Destroy when it ends.
    /// </summary>
    public sealed class VantageLetterbox
    {
        /// <summary>Bar height in reference pixels (1920×1080).</summary>
        public const float BarHeight = 130f;

        /// <summary>Full-screen black under the bars (the intro fades in from it); clear by default.</summary>
        public readonly Image Cover;

        private readonly RectTransform _canvas, _top, _bottom;
        private readonly TMP_Text _kicker, _caption;
        private float _bars;
        private int _shown = -1;

        public VantageLetterbox(string name, int order)
        {
            _canvas = VantageUI.Canvas(name, order);
            Cover = VantageUI.Image(VantageUI.Stretch("Fade", _canvas), Color.clear);
            _top = bar("Letterbox Top", 1f);
            _bottom = bar("Letterbox Bottom", 0f);
            _kicker = VantageUI.Text(_canvas, "Kicker", 20, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 84), new Vector2(1600, 24));
            _caption = VantageUI.Text(_canvas, "Caption", 34, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1600, 48));
            _kicker.characterSpacing = 6f;
            _kicker.color = _caption.color = Color.clear;
        }

        private RectTransform bar(string name, float y)
        {
            var r = VantageUI.Rect(name, _canvas, new Vector2(0.5f, y), Vector2.zero, new Vector2(4000, 0));
            r.pivot = new Vector2(0.5f, y);
            VantageUI.Image(r, Color.black);
            return r;
        }

        /// <summary>How far the bars are in, 0..1.</summary>
        public float Bars
        {
            get => _bars;
            set
            {
                _bars = Mathf.Clamp01(value);
                var size = new Vector2(4000, BarHeight * _bars);
                _top.sizeDelta = size;
                _bottom.sizeDelta = size;
            }
        }

        /// <summary>
        /// Shows subtitle 'id' (texts only rebuilt when it changes; -1 for none) at 'alpha'. Subtitles only show
        /// while the bars are (nearly) in. A title is the big white one-word line.
        /// </summary>
        public void Subtitle(int id, string kicker, string caption, float alpha, bool title = false)
        {
            if (id != _shown)
            {
                _shown = id;
                _kicker.text = id < 0 ? "" : VantageUI.Caps(kicker);
                _caption.text = id < 0 ? "" : VantageUI.Caps(caption);
                _caption.fontSize = title ? 56 : 34;
            }
            var a = id < 0 ? 0f : Mathf.Clamp01(alpha) * Mathf.Clamp01((_bars - 0.8f) / 0.2f);
            _caption.color = new Color(1, 1, 1, a);
            _kicker.color = new Color(VantageUI.Accent.r, VantageUI.Accent.g, VantageUI.Accent.b, a * 0.9f);
        }

        /// <summary>Subtitle alpha for a line shown from 'start' to 'end' at time t: quick fades like film subtitles.</summary>
        public static float Fade(float t, float start, float end) =>
            Mathf.Clamp01((t - start) / 0.2f) * Mathf.Clamp01((end - t) / 0.25f);

        public void Destroy()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
        }
    }
}
