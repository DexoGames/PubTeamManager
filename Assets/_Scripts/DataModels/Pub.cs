using UnityEngine;

[System.Serializable]
public class Pub
{
    public string FasId;
    public string Name;
    public string Address;
    public string Postcode;
    public float Easting;
    public float Northing;
    public float Latitude;
    public float Longitude;
    public string LocalAuthority;

    public Pub(string fasId, string name, string address, string postcode, 
               float easting, float northing, float latitude, float longitude, string localAuthority)
    {
        FasId = fasId;
        Name = name;
        Address = address;
        Postcode = postcode;
        Easting = easting;
        Northing = northing;
        Latitude = latitude;
        Longitude = longitude;
        LocalAuthority = localAuthority;
    }

    /// <summary>
    /// Calculate distance to another pub using Haversine formula (in kilometers)
    /// </summary>
    public float DistanceTo(Pub other)
    {
        const float earthRadius = 6371f; // Earth's radius in kilometers

        float lat1Rad = Latitude * Mathf.Deg2Rad;
        float lat2Rad = other.Latitude * Mathf.Deg2Rad;
        float deltaLatRad = (other.Latitude - Latitude) * Mathf.Deg2Rad;
        float deltaLonRad = (other.Longitude - Longitude) * Mathf.Deg2Rad;

        float a = Mathf.Sin(deltaLatRad / 2) * Mathf.Sin(deltaLatRad / 2) +
                  Mathf.Cos(lat1Rad) * Mathf.Cos(lat2Rad) *
                  Mathf.Sin(deltaLonRad / 2) * Mathf.Sin(deltaLonRad / 2);
        
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return earthRadius * c;
    }
}
