using UnityEngine;
namespace OPS
{
    /// <summary>
    /// Statistics for a pool tracking usage patterns.
    /// </summary>
    [System.Serializable]
    public class PoolStatistics
    {
        public int totalGets;
        public int totalReleases;
        public int peakActiveCount;
        public int currentActiveCount;
        public float creationTime;
        public float lastGetTime;
        public float lastReleaseTime;

        public PoolStatistics()
        {
            creationTime = Time.time;
        }

        public void RecordGet(int currentActive)
        {
            totalGets++;
            currentActiveCount = currentActive;
            lastGetTime = Time.time;

            if (currentActive > peakActiveCount)
            {
                peakActiveCount = currentActive;
            }
        }

        public void RecordRelease(int currentActive)
        {
            totalReleases++;
            currentActiveCount = currentActive;
            lastReleaseTime = Time.time;
        }

        public float GetLifetime()
        {
            return Time.time - creationTime;
        }
    }
}