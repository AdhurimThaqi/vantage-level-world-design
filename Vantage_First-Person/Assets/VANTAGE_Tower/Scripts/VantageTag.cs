using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// Holds the custom properties that were set on the object in Blender
    /// (marker_type, note, door_type, unlock_by, ...). Added automatically on import.
    public class VantageTag : MonoBehaviour
    {
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();

        public string Get(string key, string fallback = null)
        {
            int i = keys.IndexOf(key);
            return i >= 0 ? values[i] : fallback;
        }

        public bool Has(string key) => keys.Contains(key);

        public void Set(string key, string value)
        {
            int i = keys.IndexOf(key);
            if (i >= 0) values[i] = value;
            else { keys.Add(key); values.Add(value); }
        }

        public string MarkerType => Get("marker_type", "");
    }
}
