using System;

namespace AnimalObservations
{
	// An Azimuth has a value in (0 .. 360] where 0 = North (+Y) and value is clockwise degrees.  Therefore East = 90, South = 180, ...
	public struct Azimuth
	{
	    readonly double _value;

		public Azimuth (double degrees)
		{
			_value = NormalizeDegrees (degrees);
		}

		public double Value {
			get {
				return _value;
			}
		}

		private static double NormalizeDegrees (double val)
		{
			double temp = val % 360.0;
			if (temp < 0)
				temp = temp + 360.0;
			return temp;
		}

		static public Azimuth FromRadians (double radians)
		{
			return new Azimuth(radians* 180.0 / Math.PI);
		}

		//Trig degrees:  0 = +X (or East, 90 Azimuth). angle is counter clockwise.  Therefore N = 90, W = 180, S = 270
		static public Azimuth FromTrigDegrees (double degrees)
		{
			return new Azimuth(90.0 - degrees);
		}

		static public Azimuth FromTrigRadians (double radians)
		{
			return FromTrigDegrees (radians * 180.0 / Math.PI);
		}

		public double ToTrigDegrees ()
		{
			return NormalizeDegrees(90.0 - Value);
		}

		public double ToTrigRadians ()
		{
			return ToTrigDegrees () * Math.PI / 180.0;
		}

		//object overrrides
		public override bool Equals (object obj)
		{
			return obj is Azimuth && this == (Azimuth)obj;
		}

		public override int GetHashCode ()
		{
			return Value.GetHashCode () ;
		}

		public override string ToString ()
		{
			return String.Format ("{0}º",Value);
			//return Value.ToString ();
		}

		//Addition overrides
		public static Azimuth operator + (Azimuth a, Azimuth b)
		{
			return new Azimuth (a.Value + b.Value);
		}

		public static Azimuth operator - (Azimuth a, Azimuth b)
		{
			return new Azimuth (a.Value - b.Value);
		}

		public static Azimuth operator - (Azimuth a)
		{
			return new Azimuth (-1 * a.Value);
		}

		//Equality overrides
		public static bool operator == (Azimuth a, Azimuth b)
		{
			return a.Value == b.Value;
		}

		public static bool operator != (Azimuth a, Azimuth b)
		{
			return a.Value != b.Value;
		}

		//Comparison overrides
		public static bool operator < (Azimuth a, Azimuth b)
		{
			return a.Value < b.Value;
		}

		public static bool operator > (Azimuth a, Azimuth b)
		{
			return a.Value > b.Value;
		}

		public static bool operator <= (Azimuth a, Azimuth b)
		{
			return a.Value <= b.Value;
		}

		public static bool operator >= (Azimuth a, Azimuth b)
		{
			return a.Value >= b.Value;
		}

		//Conversions
		// conversion from Azimuth to double
		public static implicit operator double (Azimuth a)
		{
			return a.Value;
		}
		//  conversion from double to Azimuth
		public static implicit operator Azimuth (double d)
		{
			return new Azimuth (d);
		}

	}
}

