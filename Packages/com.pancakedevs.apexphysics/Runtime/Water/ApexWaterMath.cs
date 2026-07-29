using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Shared zero-allocation wave math used by water physics and mirrored by the
    /// included water shaders so visual and physical surface heights stay aligned.
    /// </summary>
    public static class ApexWaterMath
    {
        public const int MaxWaves = 4;

        public struct WaveLayer
        {
            public Vector2 direction;
            public float amplitude;
            public float wavelength;
            public float speed;
        }

        public static void BuildLayers(
            Vector2 windDirection,
            float waveHeight,
            float waveSpeed,
            float waveScale,
            WaveLayer[] destination)
        {
            if (destination == null)
            {
                return;
            }

            if (windDirection.sqrMagnitude < 0.0001f)
            {
                windDirection = Vector2.right;
            }
            windDirection.Normalize();

            int count = Mathf.Min(MaxWaves, destination.Length);
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 27f - 40f) * Mathf.Deg2Rad;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                Vector2 direction = new Vector2(
                    windDirection.x * cosine - windDirection.y * sine,
                    windDirection.x * sine + windDirection.y * cosine).normalized;

                float frequencyMultiplier = 1f + i * 0.63f;
                destination[i] = new WaveLayer
                {
                    direction = direction,
                    amplitude = waveHeight * (1f / (i + 1f)) * 0.6f,
                    wavelength = Mathf.Max(0.01f, waveScale / frequencyMultiplier),
                    speed = waveSpeed * (0.7f + i * 0.15f)
                };
            }

            for (int i = count; i < destination.Length; i++)
            {
                destination[i] = default;
            }
        }

        public static float SampleHeight(Vector3 worldPosition, float time, WaveLayer[] layers)
        {
            if (layers == null)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < layers.Length; i++)
            {
                WaveLayer layer = layers[i];
                if (layer.wavelength <= 0f || layer.amplitude == 0f)
                {
                    continue;
                }

                float waveNumber = 2f * Mathf.PI / layer.wavelength;
                float phase = Vector2.Dot(
                    layer.direction,
                    new Vector2(worldPosition.x, worldPosition.z)) * waveNumber +
                    time * layer.speed;
                height += layer.amplitude * Mathf.Sin(phase);
            }

            return height;
        }
    }
}
