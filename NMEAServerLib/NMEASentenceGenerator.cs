﻿using System;
using System.Text;

namespace NMEAServerLib
{
    public class NMEASentenceGenerator
    {
        private enum Coord { Lat, Lon }

        public static String generate(InstrumentsData data)
        {
            string nmea = "";

            if (data.Lat != null && data.Lon != null) nmea += generatePositionSentence_GLL(data.Lat ?? 0, data.Lon ?? 0);
            if (data.TrueHeading != null && data.Speed != null) nmea += generateSpeedAndHeadingSentence_VHW(data.Speed ?? 0, data.TrueHeading ?? 0, data.MagneticHeading);
            if (data.TrueHeading != null) nmea += generateTrueHeadingSentence_HDT(data.TrueHeading ?? 0);

            return nmea;
        }

        public static string generatePositionSentence_GLL(double lat, double lon)
        {
            string _lat = ConvertDecimalDegreesToNMEAFormat(lat, Coord.Lat);
            string _lon = ConvertDecimalDegreesToNMEAFormat(lon, Coord.Lon);
            string sentence = "GPGLL," + _lat + "," + _lon + "," + UTCTime() + ",A";
            return FormatSentence(sentence);
        }

        public static string generateSpeedAndHeadingSentence_VHW(double speed, double trueHeading, Nullable<double> magneticHeading)
        {
            string _heading = trueHeading.ToString().Replace(",", ".");
            if (magneticHeading == null) magneticHeading = trueHeading;
            string _speedKnots = speed.ToString().Replace(",", ".");
            string _speedKmH = (speed * 1.852).ToString().Replace(",", ".");
            string sentence = "IIVHW," + _heading + ",T," + _heading + ",M," + _speedKnots + ",N," + _speedKmH + ",K";
            return FormatSentence(sentence);
        }

        public static string generateTrueHeadingSentence_HDT(double trueHeading)
        {
            string _heading = trueHeading.ToString().Replace(",", ".");
            string sentence = "IIHDT," + _heading + ",T";
            return FormatSentence(sentence);
        }

        private static string CalculateCheckSum(String sentence)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(sentence);

            byte checkSumByte = 0x00;
            for (int i = 0; i < bytes.Length; i++) checkSumByte ^= bytes[i];

            return BitConverter.ToString(new byte[1] { checkSumByte });
        }

        private static string FormatSentence(string sentence)
        {
            return "$" + sentence + "*" + CalculateCheckSum(sentence) + "\n";
        }

        private static string ConvertDecimalDegreesToNMEAFormat(double decimalDegrees, Coord coord)
        {
            String cardinal = "";

            switch (coord)
            {
                case Coord.Lat:
                    cardinal = decimalDegrees >= 0 ? "N" : "S";
                    break;
                case Coord.Lon:
                    cardinal = decimalDegrees >= 0 ? "E" : "W";
                    break;
            }

            decimalDegrees = Math.Abs(decimalDegrees);

            double sec = decimalDegrees * 3600;
            int deg = (int)Math.Floor(sec / 3600);
            sec -= deg * 3600;
            double min = sec / 60;

            string test = deg.ToString("00") + min.ToString("00.0#####").Replace(",", ".") + "," + cardinal;
            return deg.ToString("00") + min.ToString("00.0#####").Replace(",", ".") + "," + cardinal;
        }

        private static string UTCTime()
        {
            DateTime utc = DateTime.UtcNow;
            return DateTime.UtcNow.ToString("HHmmss.fff");
        }
    }
}