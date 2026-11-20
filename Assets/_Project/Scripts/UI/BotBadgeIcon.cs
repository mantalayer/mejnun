using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class BotBadgeIcon : VisualElement
    {
        private static readonly Color FaceColor = new Color(177f / 255f, 224f / 255f, 120f / 255f);
        private static readonly Color DetailColor = new Color(22f / 255f, 32f / 255f, 24f / 255f);

        public BotBadgeIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var painter = context.painter2D;
            var left = rect.xMin;
            var top = rect.yMin;
            var width = rect.width;
            var height = rect.height;
            painter.lineWidth = Mathf.Max(1f, Mathf.Min(width, height) * 0.07f);

            painter.BeginPath();
            painter.MoveTo(new Vector2(left + width * 0.5f, top + height * 0.29f));
            painter.LineTo(new Vector2(left + width * 0.5f, top + height * 0.14f));
            painter.strokeColor = FaceColor;
            painter.Stroke();

            painter.BeginPath();
            painter.MoveTo(new Vector2(left + width * 0.5f, top + height * 0.07f));
            painter.LineTo(new Vector2(left + width * 0.57f, top + height * 0.14f));
            painter.LineTo(new Vector2(left + width * 0.5f, top + height * 0.21f));
            painter.LineTo(new Vector2(left + width * 0.43f, top + height * 0.14f));
            painter.ClosePath();
            painter.fillColor = FaceColor;
            painter.Fill();

            painter.BeginPath();
            painter.MoveTo(new Vector2(left + width * 0.26f, top + height * 0.29f));
            painter.LineTo(new Vector2(left + width * 0.74f, top + height * 0.29f));
            painter.LineTo(new Vector2(left + width * 0.84f, top + height * 0.39f));
            painter.LineTo(new Vector2(left + width * 0.84f, top + height * 0.76f));
            painter.LineTo(new Vector2(left + width * 0.74f, top + height * 0.86f));
            painter.LineTo(new Vector2(left + width * 0.26f, top + height * 0.86f));
            painter.LineTo(new Vector2(left + width * 0.16f, top + height * 0.76f));
            painter.LineTo(new Vector2(left + width * 0.16f, top + height * 0.39f));
            painter.ClosePath();
            painter.fillColor = FaceColor;
            painter.Fill();
            painter.strokeColor = DetailColor;
            painter.Stroke();

            painter.BeginPath();
            painter.MoveTo(new Vector2(left + width * 0.32f, top + height * 0.48f));
            painter.LineTo(new Vector2(left + width * 0.43f, top + height * 0.48f));
            painter.LineTo(new Vector2(left + width * 0.43f, top + height * 0.59f));
            painter.LineTo(new Vector2(left + width * 0.32f, top + height * 0.59f));
            painter.ClosePath();
            painter.fillColor = DetailColor;
            painter.Fill();

            painter.BeginPath();
            painter.MoveTo(new Vector2(left + width * 0.57f, top + height * 0.48f));
            painter.LineTo(new Vector2(left + width * 0.68f, top + height * 0.48f));
            painter.LineTo(new Vector2(left + width * 0.68f, top + height * 0.59f));
            painter.LineTo(new Vector2(left + width * 0.57f, top + height * 0.59f));
            painter.ClosePath();
            painter.fillColor = DetailColor;
            painter.Fill();

            painter.BeginPath();
            painter.MoveTo(new Vector2(left + width * 0.34f, top + height * 0.71f));
            painter.LineTo(new Vector2(left + width * 0.66f, top + height * 0.71f));
            painter.strokeColor = DetailColor;
            painter.Stroke();
        }
    }
}