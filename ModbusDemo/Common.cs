using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    /*数据类型*/
    [System.Flags]
    public enum DataType
    {
        Bool = 0x01,
        Byte = 0x02,
        Char = 0x03,
        /*2字节有符号整数*/
        Short = 0x04,
        Word = 0x05,
        BCD = 0x06,
        /*4字节有符号整数*/
        Long = 0x07,
        DWord = 0x08,
        LBCD = 0x09,
        Float = 0x0a,
        Double = 0x0b,
        Date = 0x0c,
        LongLong = 0x0d,
        WString = 0x0e,

        Array = 0x10000000

    }
    /*质量戳*/
    [System.Flags]
    public enum Quality
    {
        badNonSpecific = 0x00,
        noQualityNoValue = 0xff,
        goodNonSpecific = 0xc0,

        limitFieldNot = 0x0,
        limitFieldLow = 0x1,
        limitFieldHigh = 0x2,
        limitFieldConstant = 0x3,

    }
    /*错误码*/
    [System.Flags]
    public enum ErrorCode
    {
         noError = 0x00
    }

    public enum Switch
    {
        Off = 0x00,
        On  = 0x01
    }

    /*数据基类*/
    public abstract class Data
    {
        public Data(DataType type)
        {
            isArray = false;
            arrayLength = 0;
            dataType = type;
            timestamp = DateTime.Now.Millisecond;
            quality = Quality.noQualityNoValue;
            errorCode = ErrorCode.noError;
        }
        public Data(int arrayLen, DataType type) : this(type)
        {
            isArray = true;
            arrayLength = arrayLen;
            if (arrayLen <= 0) throw new ArgumentException("数据类型数组长度必须大于0！");
        }
        public byte[] rawData { get; set; }
        /*数据类型*/
        public DataType dataType { get; }
        /*时间戳*/
        public long timestamp { get; set; }
        public ErrorCode errorCode { get; set; }
        /*质量戳*/
        public Quality quality { get; set; }

        public bool isArray { get; }
        public int arrayLength{get; }

        public bool setRawData(byte[] data)
        {
            int needSize = getDataLength();
            if (data == null || data.Length != needSize) return false;
            if (rawData == null) rawData = new byte[needSize];

            Array.Copy(data, rawData, needSize);

            timestamp = DateTime.Now.Millisecond;

            return true;
        }
        public byte[] getRawData()
        {
            if (rawData == null) return new byte[getDataLength()];

            return rawData;
        }
        public abstract short getDataTypeLength();
        public int getDataLength()
        {
            return getDataTypeLength() * (isArray?arrayLength:1);
        }

        public override string ToString()
        {
            if (rawData == null) return "无数据";
            StringBuilder builder = new StringBuilder();

            for(int i = 0; i < rawData.Length; ++i)
            {
                builder.Append( rawData[i].ToString("X2")).Append(" ");
            }

            return builder.ToString();
        }
    }

    public class DataLong : Data
    {
        public DataLong() : base(DataType.Long) { }
        public DataLong(int arrayLen) : base(arrayLen, DataType.Long) { }

        public override short getDataTypeLength()
        {
            return 4;
        }
    }
    public class DataShort : Data
    {
        public DataShort() : base(DataType.Short) { }
        public DataShort(int arrayLen) : base(arrayLen, DataType.Short) { }

        public override short getDataTypeLength()
        {
            return 2;
        }
    }
    public class DataBool : Data
    {
        public DataBool() : base(DataType.Bool) { }
        public DataBool(int arrayLen) : base(arrayLen, DataType.Bool) { }

        public override short getDataTypeLength()
        {
            return 1;
        }
    }
    public class DataFloat : Data
    {
        public DataFloat() : base(DataType.Float) { }
        public DataFloat(int arrayLen) : base(arrayLen, DataType.Float) { }

        public override short getDataTypeLength()
        {
            return 4;
        }
    }
    public class DataDouble : Data
    {
        public DataDouble() : base(DataType.Double) { }
        public DataDouble(int arrayLen) : base(arrayLen, DataType.Double) { }

        public override short getDataTypeLength()
        {
            return 8;
        }
    } 
    public class DataFactory <T> where T:Tag
    {
        public static  Data createData(T tag)
        {
            if (tag == null) return null;
            switch (tag.dataType)
            {
                case DataType.Bool: return new DataBool();
                case DataType.Short:return new DataShort();
                case DataType.Long: return new DataLong();
                case DataType.Float: return new DataFloat();
                case DataType.Double: return new DataDouble();
            }

            return null;
        }
    }

    public abstract class Tag
    {
        public string name { get; set; }
        public DataType dataType { get; set; }
        public abstract bool parserConfig();
        public abstract Data read(Object client);
    }

    public class MQTTConfig
    {
        public string brokerAddress { get; set; } = "127.0.0.1";
        public int port { get; set; } = 1883;
        public string clientId { get; set; } = "modbus";

        public string userName { get; set; } = "modbus";
        public string password { get; set; } = "123456";
        public bool cleanSession { get; set; } = true;

        public string pubTopic { get; set; } = "modbusTopic";

    }


}
