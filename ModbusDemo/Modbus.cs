using System;
using System.Collections.Generic;
using System.Text;
using Common;
using HslCommunication;
using HslCommunication.ModBus;

namespace Modbus
{
    public enum AddressType
    {
        outputCoils = 1,
        inputCoils = 2,
        internalRegisters = 3,
        holdingRegisters = 4
    }
    public class ModbusTcpTag : Tag
    {
        public int addressType { get; set; }
        public int baseAddress { get; set; } = 0;
        public int bit { get; set; } = 0;
        public override bool parserConfig()
        {
            if (baseAddress >= 1) baseAddress -= 1;


            return true;
        }

        public override Data read(object _client)
        {
            // 创建数据
            var data = DataFactory<ModbusTcpTag>.createData(this);
            if (_client is ModbusTcpNet)
            {
                ModbusTcpNet client = (ModbusTcpNet)_client;
                           
                ushort len = (ushort)(data.getDataLength());
                switch (addressType)
                {
                    case (int)AddressType.inputCoils: // 离散输入 02命令 
                    case (int)AddressType.outputCoils: // 线圈 01命令 readCoil
                        {
                            OperateResult<bool[]> result;
                            if(addressType == (int)AddressType.inputCoils)
                                result  = client.ReadDiscrete(baseAddress.ToString(), len);
                            else
                                result = client.ReadCoil(baseAddress.ToString(), len);
                            data.quality = result.IsSuccess ? Quality.goodNonSpecific : Quality.badNonSpecific;
                            if (result.IsSuccess) { 
                                byte[] raw = new byte[len];
                                for (int i = 0; i < len; ++i) raw[i] = (byte)(result.Content[i]?Switch.On:Switch.Off);
                                data.setRawData(raw);
                            }
                            else
                            {
                                Console.WriteLine($"{name}:{baseAddress},{len}:读取失败[{result.Message}]");
                            }
                        }
                        break;
                    case (int)AddressType.holdingRegisters: // 保持寄存器 04命令
                    case (int)AddressType.internalRegisters:
                        {
                            String a = name;
                            len /= 2;
                            String readP1;
                            if(addressType == (int)AddressType.internalRegisters) 
                                readP1 = $"x=4;{baseAddress}";
                            else 
                                readP1 = (baseAddress).ToString();
                            OperateResult<byte[]> result = client.Read(readP1, len);
                            data.quality = result.IsSuccess ? Quality.goodNonSpecific : Quality.badNonSpecific;
                            if (result.IsSuccess)
                            {
                                data.setRawData(result.Content);
                            }
                            else
                            {
                                Console.WriteLine($"{name}:{readP1},{len}:读取失败[{result.Message}]");
                            }
                        }
                        break;                        

                    default:break;
                }
            }
            return data;
        }
    }

    public class ModbusTcpConfig<T>
    {
        public string ip { get; set; }
        public int port { get; set; }
        // 0-255
        public byte slaveId { get; set; }
        // 设备地址是否从0开始
        public bool addressStartWithZero { get; set; }

        public List<T> tags { get; set; }
    }
}
