using UnityEngine;
using UnityEngine.UIElements;

// Pixel flames that burn behind the streak multiplier. UI Toolkit has no
// particle system, so this is a tiny CPU one: square specks rise from the foot
// of the number, sway, shrink and burn through a colour ramp before fading.
// The multiplier tier decides how many there are, how big, how high they
// climb and which ramps they use; x1 burns nothing.
public class StreakFlames : VisualElement
{
    private struct Speck
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public float SwayPhase;
        public Color32[] Ramp;
    }

    private struct TierLook
    {
        public float Rate;       // specks per second
        public float MinSize, MaxSize;
        public float MinSpeed, MaxSpeed;
        public float MinLife, MaxLife;
        public Color32[][] Ramps; // picked round-robin, so two ramps split evenly
    }

    // Colour over a speck's life, hottest first
    private static readonly Color32[] Yellow =
    {
        new Color32(255, 255, 214, 255), new Color32(255, 232, 72, 255),
        new Color32(255, 176, 34, 255), new Color32(204, 96, 22, 255),
    };
    private static readonly Color32[] Orange =
    {
        new Color32(255, 226, 120, 255), new Color32(255, 150, 30, 255),
        new Color32(232, 72, 22, 255), new Color32(124, 30, 20, 255),
    };
    private static readonly Color32[] Blue =
    {
        new Color32(226, 246, 255, 255), new Color32(92, 192, 255, 255),
        new Color32(42, 94, 232, 255), new Color32(44, 30, 142, 255),
    };

    // Indexed by multiplier; x1 and anything unknown burn nothing
    private static readonly TierLook[] Looks =
    {
        default,
        default,
        new TierLook { Rate = 22f, MinSize = 2f, MaxSize = 3f, MinSpeed = 22f, MaxSpeed = 38f, MinLife = 0.30f, MaxLife = 0.50f, Ramps = new[] { Yellow } },
        new TierLook { Rate = 42f, MinSize = 2f, MaxSize = 4f, MinSpeed = 30f, MaxSpeed = 54f, MinLife = 0.40f, MaxLife = 0.65f, Ramps = new[] { Orange } },
        new TierLook { Rate = 70f, MinSize = 3f, MaxSize = 5f, MinSpeed = 38f, MaxSpeed = 68f, MinLife = 0.45f, MaxLife = 0.80f, Ramps = new[] { Blue, Orange } },
    };

    private const int MaxSpecks = 160;
    private const int TickMs = 16;
    // How far above the foot of the number the whole fire sits
    private const float LiftPx = 4f;

    private readonly Speck[] _specks = new Speck[MaxSpecks];
    private int _count;
    private int _tier;
    private float _emitCarry;
    private int _nextRamp;
    // Its own stream, so HUD sparkle never shifts the rolls wave selection makes
    private readonly System.Random _random = new System.Random();
    private readonly IVisualElementScheduledItem _ticker;

    public StreakFlames()
    {
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0;
        style.top = -LiftPx;
        style.right = 0;
        style.bottom = LiftPx;
        generateVisualContent += Draw;
        _ticker = schedule.Execute(Tick).Every(TickMs);
        _ticker.Pause();
    }

    public void SetTier(int multiplier)
    {
        _tier = multiplier;
        // Specks already in the air finish burning out after a drop to x1, so
        // the ticker keeps running until the last one is gone
        if (Burning || _count > 0) _ticker.Resume();
    }

    private bool Burning => _tier >= 2 && _tier < Looks.Length;

    private void Tick(TimerState timer)
    {
        // A hitch (or a long pause) shouldn't fire a burst of specks all at once
        float dt = Mathf.Min(timer.deltaTime / 1000f, 0.05f);

        for (int i = _count - 1; i >= 0; i--)
        {
            ref Speck speck = ref _specks[i];
            speck.Age += dt;
            if (speck.Age >= speck.Life)
            {
                _specks[i] = _specks[--_count];
                continue;
            }
            speck.Position += speck.Velocity * dt;
            speck.Position.x += Mathf.Sin(speck.Age * 14f + speck.SwayPhase) * 10f * dt;
        }

        if (Burning)
        {
            _emitCarry += Looks[_tier].Rate * dt;
            while (_emitCarry >= 1f)
            {
                _emitCarry -= 1f;
                Spawn(Looks[_tier]);
            }
        }
        else
        {
            _emitCarry = 0f;
            if (_count == 0) _ticker.Pause();
        }

        MarkDirtyRepaint();
    }

    private void Spawn(TierLook look)
    {
        float width = contentRect.width;
        float height = contentRect.height;
        if (_count >= MaxSpecks || width <= 1f || height <= 1f) return;

        // Two rolls averaged bunch the specks toward the middle of the number,
        // so the fire reads as one flame rather than a flat row of sparks
        float across = (Roll() + Roll()) * 0.5f;
        _specks[_count++] = new Speck
        {
            Position = new Vector2(Mathf.Lerp(width * 0.05f, width * 0.95f, across), height * (0.78f + Roll() * 0.12f)),
            Velocity = new Vector2((Roll() - 0.5f) * 14f, -Mathf.Lerp(look.MinSpeed, look.MaxSpeed, Roll())),
            Life = Mathf.Lerp(look.MinLife, look.MaxLife, Roll()),
            Size = Mathf.Lerp(look.MinSize, look.MaxSize, Roll()),
            SwayPhase = Roll() * Mathf.PI * 2f,
            Ramp = look.Ramps[_nextRamp++ % look.Ramps.Length],
        };
    }

    private float Roll() => (float)_random.NextDouble();

    private void Draw(MeshGenerationContext context)
    {
        if (_count == 0) return;

        MeshWriteData mesh = context.Allocate(_count * 4, _count * 6);
        for (int i = 0; i < _count; i++)
        {
            Speck speck = _specks[i];
            float t = speck.Age / speck.Life;
            Color32 colour = Sample(speck.Ramp, t);
            // Solid for most of the climb, then gutters out
            colour.a = (byte)(255f * Mathf.Clamp01((1f - t) / 0.4f));

            // Whole-pixel squares on whole-pixel positions keep the pixel-art look
            float size = Mathf.Max(1f, Mathf.Round(speck.Size * (1f - 0.5f * t)));
            float x = Mathf.Round(speck.Position.x - size * 0.5f);
            float y = Mathf.Round(speck.Position.y - size * 0.5f);

            // Clockwise in UI space (y down), as UI Toolkit expects
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = colour });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + size, y, Vertex.nearZ), tint = colour });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + size, y + size, Vertex.nearZ), tint = colour });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y + size, Vertex.nearZ), tint = colour });

            ushort first = (ushort)(i * 4);
            mesh.SetNextIndex(first);
            mesh.SetNextIndex((ushort)(first + 1));
            mesh.SetNextIndex((ushort)(first + 2));
            mesh.SetNextIndex(first);
            mesh.SetNextIndex((ushort)(first + 2));
            mesh.SetNextIndex((ushort)(first + 3));
        }
    }

    private static Color32 Sample(Color32[] ramp, float t)
    {
        float scaled = Mathf.Clamp01(t) * (ramp.Length - 1);
        int index = Mathf.Min((int)scaled, ramp.Length - 2);
        return Color32.Lerp(ramp[index], ramp[index + 1], scaled - index);
    }
}
