using NUnit.Framework;

namespace com.mjgame.nmd.nkl.tests
{
    public class TextFormatTests
    {
        readonly char[] _buffer = new char[256];

        string Number(long value, char separator = ',') =>
            TextFormat.ToString(_buffer, TextFormat.WriteNumber(_buffer, 0, value, separator));

        string Abbreviated(long value, int decimals = 1, long from = 1000) =>
            TextFormat.ToString(_buffer, TextFormat.WriteAbbreviated(_buffer, 0, value, decimals, from));

        string Time(double seconds, TimeFormat format, bool roundUp = false, TimeLabels labels = null) =>
            TextFormat.ToString(_buffer, TextFormat.WriteTime(_buffer, 0, seconds, format, roundUp, labels));

        string Decimal(double value, int decimals) =>
            TextFormat.ToString(_buffer, TextFormat.WriteDecimal(_buffer, 0, value, decimals));

        string Percent(double ratio, int decimals = 0) =>
            TextFormat.ToString(_buffer, TextFormat.WritePercent(_buffer, 0, ratio, decimals));

        [TestCase(0, ExpectedResult = "0")]
        [TestCase(7, ExpectedResult = "7")]
        [TestCase(999, ExpectedResult = "999")]
        [TestCase(1000, ExpectedResult = "1,000")]
        [TestCase(1234567, ExpectedResult = "1,234,567")]
        [TestCase(-1234, ExpectedResult = "-1,234")]
        [TestCase(-999, ExpectedResult = "-999")]
        [TestCase(long.MaxValue, ExpectedResult = "9,223,372,036,854,775,807")]
        [TestCase(long.MinValue, ExpectedResult = "-9,223,372,036,854,775,808")]
        public string WriteNumber(long value) => Number(value);

        [Test]
        public void WriteNumber_CustomAndNoSeparator()
        {
            Assert.AreEqual("1.234.567", Number(1234567, '.'));
            Assert.AreEqual("1234567", Number(1234567, '\0'));
        }

        [TestCase(0, ExpectedResult = "0")]
        [TestCase(999, ExpectedResult = "999")]
        [TestCase(1000, ExpectedResult = "1k")]
        [TestCase(1500, ExpectedResult = "1.5k")]
        [TestCase(1999, ExpectedResult = "1.9k")]
        [TestCase(15300, ExpectedResult = "15.3k")]
        [TestCase(150000, ExpectedResult = "150k")]
        [TestCase(999999, ExpectedResult = "999k")]
        [TestCase(1000000, ExpectedResult = "1M")]
        [TestCase(1250000, ExpectedResult = "1.2M")]
        [TestCase(3000000000, ExpectedResult = "3B")]
        [TestCase(-2500, ExpectedResult = "-2.5k")]
        [TestCase(long.MaxValue, ExpectedResult = "9.2Qi")]
        [TestCase(long.MinValue, ExpectedResult = "-9.2Qi")]
        public string WriteAbbreviated(long value) => Abbreviated(value);

        [Test]
        public void WriteAbbreviated_Decimals()
        {
            Assert.AreEqual("1.25M", Abbreviated(1250000, 2));
            Assert.AreEqual("1.234k", Abbreviated(1234, 3));
            Assert.AreEqual("1k", Abbreviated(1050, 0));
            Assert.AreEqual("1.05k", Abbreviated(1050, 2));
            Assert.AreEqual("1.5k", Abbreviated(1500, 3), "trailing zeros are dropped");
        }

        string AbbreviatedDouble(double value, int decimals = 1) =>
            TextFormat.ToString(_buffer, TextFormat.WriteAbbreviated(_buffer, 0, value, decimals));

        [Test]
        public void WriteAbbreviated_Double_IdleUnits()
        {
            Assert.AreEqual("999", AbbreviatedDouble(999.9));
            Assert.AreEqual("1k", AbbreviatedDouble(1000));
            Assert.AreEqual("1.3M", AbbreviatedDouble(1.3e6), "binary rounding must not give 1.2M");
            Assert.AreEqual("1Qi", AbbreviatedDouble(1e18));
            Assert.AreEqual("1.5Sx", AbbreviatedDouble(1.5e21));
            Assert.AreEqual("12Sp", AbbreviatedDouble(1.2e25));
            Assert.AreEqual("999Oc", AbbreviatedDouble(9.99e29));
            Assert.AreEqual("1Dc", AbbreviatedDouble(1e33));
            Assert.AreEqual("2.5Vg", AbbreviatedDouble(2.5e63));
            Assert.AreEqual("1000Vg", AbbreviatedDouble(1e66), "past the last unit the whole part grows");
            Assert.AreEqual("-4.2Td", AbbreviatedDouble(-4.2e42));
            Assert.AreEqual("0", AbbreviatedDouble(double.NaN));
            Assert.AreEqual("∞", AbbreviatedDouble(double.PositiveInfinity));
        }

        [Test]
        public void WriteAbbreviated_DoubleMatchesLong()
        {
            long[] values = { 1000, 1500, 1999, 15300, 999999, 1250000, 3000000000, 123456789012345 };
            foreach (long v in values)
            {
                string fromLong = Abbreviated(v);
                Assert.AreEqual(fromLong, AbbreviatedDouble(v), "value " + v);
            }
        }

        [Test]
        public void WriteAbbreviated_From()
        {
            Assert.AreEqual("9,999", Abbreviated(9999, 1, 10000));
            Assert.AreEqual("10k", Abbreviated(10000, 1, 10000));
        }

        [TestCase(0, ExpectedResult = "00:00")]
        [TestCase(5, ExpectedResult = "00:05")]
        [TestCase(75, ExpectedResult = "01:15")]
        [TestCase(3599, ExpectedResult = "59:59")]
        [TestCase(3600, ExpectedResult = "1:00:00")]
        [TestCase(3725, ExpectedResult = "1:02:05")]
        [TestCase(90061, ExpectedResult = "25:01:01")]
        [TestCase(-5, ExpectedResult = "00:00")]
        public string WriteTime_Auto(double seconds) => Time(seconds, TimeFormat.Auto);

        [Test]
        public void WriteTime_Formats()
        {
            Assert.AreEqual("75:00", Time(4500, TimeFormat.MinutesSeconds));
            Assert.AreEqual("0:01:15", Time(75, TimeFormat.HoursMinutesSeconds));
            Assert.AreEqual("00:59", Time(59.9, TimeFormat.Auto), "floors by default");
            Assert.AreEqual("01:00", Time(59.1, TimeFormat.Auto, roundUp: true));
            Assert.AreEqual("00:01", Time(0.2, TimeFormat.Auto, roundUp: true));
        }

        [TestCase(0, ExpectedResult = "0s")]
        [TestCase(9, ExpectedResult = "9s")]
        [TestCase(310, ExpectedResult = "5m 10s")]
        [TestCase(300, ExpectedResult = "5m")]
        [TestCase(12000, ExpectedResult = "3h 20m")]
        [TestCase(7200, ExpectedResult = "2h")]
        [TestCase(97200, ExpectedResult = "1d 3h")]
        [TestCase(86400, ExpectedResult = "1d")]
        public string WriteTime_Compact(double seconds) => Time(seconds, TimeFormat.Compact);

        [Test]
        public void WriteTime_CompactLocalized()
        {
            var vi = new TimeLabels(" ngày", " giờ", " phút", " giây");
            Assert.AreEqual("1 ngày 3 giờ", Time(97200, TimeFormat.Compact, labels: vi));
        }

        [Test]
        public void WriteDecimal()
        {
            Assert.AreEqual("3.14", Decimal(3.14159, 2));
            Assert.AreEqual("3", Decimal(2.5, 0), "rounds half away from zero");
            Assert.AreEqual("-1.50", Decimal(-1.5, 2));
            Assert.AreEqual("0.00", Decimal(-0.001, 2), "no negative zero");
            Assert.AreEqual("0", Decimal(double.NaN, 0));
        }

        [Test]
        public void WritePercent()
        {
            Assert.AreEqual("76%", Percent(0.756));
            Assert.AreEqual("75.6%", Percent(0.756, 1));
            Assert.AreEqual("0%", Percent(0));
            Assert.AreEqual("100%", Percent(1));
            Assert.AreEqual("150%", Percent(1.5));
        }

        [Test]
        public void WriteFraction()
        {
            int length = TextFormat.WriteFraction(_buffer, 0, 1250, 5000);
            Assert.AreEqual("1,250/5,000", TextFormat.ToString(_buffer, length));
        }

        [Test]
        public void WritesAtIndex()
        {
            int i = TextFormat.WriteString(_buffer, 0, "$ ");
            i = TextFormat.WriteNumber(_buffer, i, 1000);
            i = TextFormat.WriteString(_buffer, i, " coins");
            Assert.AreEqual("$ 1,000 coins", TextFormat.ToString(_buffer, i));
        }
    }
}
