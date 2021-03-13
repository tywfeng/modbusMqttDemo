using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Common;
using HslCommunication;
using HslCommunication.ModBus;
using Modbus;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

// NuGet MQTTNet
// Install-Package MQTTnet -Version 3.0.15
namespace ModbusDemo
{
    public class Config
    {
        public MQTTConfig mqtt { get; set; }
        public ModbusTcpConfig<ModbusTcpTag> modbus { get; set; }
    }
    class Program
    {
        public static volatile bool gThreadRunning = false;
        public static ConcurrentDictionary<Tag, Data> gData = new ConcurrentDictionary<Tag, Data>();
        static void Main(string[] args)
        {
            gThreadRunning = true;
            

            Config fileConfig = null;
            try
            {
                using (StreamReader sr = new StreamReader("Modbus.conf"))
                {
                    fileConfig = JsonSerializer.Deserialize<Config>(sr.ReadToEnd());
                }
            }
            catch (Exception e)
            {
            }
            if(fileConfig == null)
            {
                Console.WriteLine("配置文件读取失败");
                return;
            }

            Console.WriteLine("启动采集");
            AcThread acThread = new AcThread(fileConfig);
            Thread acthread = new Thread(new ThreadStart(acThread.AcRunnable));
            acthread.Start();

            Console.WriteLine("启动发布");
            PubThread pubThread = new PubThread(fileConfig);
            Thread pubthread = new Thread(new ThreadStart(pubThread.PubRunnable));
            pubthread.Start();


            Console.ReadKey();
            Console.WriteLine("停止任务");
            gThreadRunning = false;
            Console.ReadKey();
        }




    }


    class AcThread
    {
        private ModbusTcpNet modbusClient = new ModbusTcpNet();
        private Config config;

        public AcThread(Config config)
        {
            this.config = config;
        }

        /*数据采集线程*/
        public void AcRunnable()
        {
            modbusClient = new ModbusTcpNet(config.modbus.ip, config.modbus.port, config.modbus.slaveId);
            modbusClient.AddressStartWithZero = config.modbus.addressStartWithZero;

            while (Program.gThreadRunning)
            {

                if (!modbusClient.ConnectServer().IsSuccess)
                {
                    Console.WriteLine("连接失败 500ms后重连");
                }
                else
                {
                    Console.WriteLine("连接成功");
                    break;
                }

                Thread.Sleep(500);

            }
            while (Program.gThreadRunning)
            {
                foreach (Tag tag in config.modbus.tags)
                {
                    try
                    {

                        Data data = tag.read(modbusClient);
                        Program.gData[tag] = data;
                    }
                    catch (Exception e)
                    {
                        Program.gData[tag] = null;
                    }
                }
                Thread.Sleep(2000);
            }

            Console.WriteLine("数据采集任务退出");
        }

    }

    class PubThread
    {
        private Config config;
        public PubThread(Config config)
        {
            this.config = config;
        }
        public async void PubRunnable()
        {
            Console.WriteLine("连接MQTT服务器中...");

            var mqttOpts = new MqttClientOptionsBuilder().WithClientId(config.mqtt.clientId)
                .WithTcpServer(config.mqtt.brokerAddress, config.mqtt.port)
                .WithCredentials(config.mqtt.userName, config.mqtt.password)
                //.WithTls()
                .WithCleanSession(config.mqtt.cleanSession)
                .Build();
            var client = new MqttFactory().CreateMqttClient();
            client.UseDisconnectedHandler(
                async e =>
                {
                    Console.WriteLine("与MQTT服务器连接断开！");
                    Console.WriteLine("500ms后自动重连！");
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    try
                    {
                        await client.ConnectAsync(mqttOpts, CancellationToken.None);
                    }
                    catch
                    {

                    }
                }
                );
            try
            {
                await client.ConnectAsync(mqttOpts, CancellationToken.None);
            }
            catch (Exception e)
            {

            }

            Console.WriteLine("MQTT服务器连接成功！");

            var pubData = new Dictionary<String, String>();
            foreach (Tag tag in config.modbus.tags)
            {
                pubData[tag.name] = "";
            }

            while (Program.gThreadRunning)
            {
                foreach (Tag tag in config.modbus.tags)
                {
                    Data data = Program.gData.GetValueOrDefault(tag, null);
                    if (data != null)
                        pubData[tag.name] = data.ToString();
                    else
                        pubData[tag.name] = "";
                }
                String payload = JsonSerializer.Serialize(pubData);

                Console.WriteLine(payload);
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic(config.mqtt.pubTopic)
                    .WithPayload(payload)
                    .WithExactlyOnceQoS()
                    .WithRetainFlag()
                    .Build();
                try
                {
                    await client.PublishAsync(msg, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {

                }
                Thread.Sleep(2000);
            }
            Console.WriteLine("数据发布任务退出");
        }
    }
}

/**
 
/*   ModbusTcpConfig<ModbusTcpTag> config = new ModbusTcpConfig<ModbusTcpTag>();
               config.ip = "127.0.0.1";
               config.port = 502;
               config.slaveId = 1;
               config.addressStartWithZero = true;
               config.tags = new List<ModbusTcpTag>();

               // 从配置中读取点配置
               ModbusTcpTag tag1 = new ModbusTcpTag();
               tag1.addressType = (int)AddressType.internalRegisters;
               tag1.name = "环境温度";
               tag1.dataType = DataType.Short;
               tag1.baseAddress = 1;
               tag1.bit = 0;
               tag1.parserConfig();
               config.tags.Add(tag1);

               ModbusTcpTag tag2 = new ModbusTcpTag();
               tag2.addressType = (int)AddressType.internalRegisters;
               tag2.name = "环境湿度";
               tag2.dataType = DataType.Short;
               tag2.baseAddress = 2;
               tag2.bit = 0;
               tag2.parserConfig();
               config.tags.Add(tag2);

               ModbusTcpTag tag3 = new ModbusTcpTag();
               tag3.addressType = (int)AddressType.internalRegisters;
               tag3.name = "电机转速";
               tag3.dataType = DataType.Long;
               tag3.baseAddress = 3;
               tag3.bit = 0;
               tag3.parserConfig();
               config.tags.Add(tag3);

               ModbusTcpTag tag4 = new ModbusTcpTag();
               tag4.addressType = (int)AddressType.internalRegisters;
               tag4.name = "电流";
               tag4.dataType = DataType.Float;
               tag4.baseAddress = 5;
               tag4.bit = 0;
               tag4.parserConfig();
               config.tags.Add(tag4);

               ModbusTcpTag tag5 = new ModbusTcpTag();
               tag5.addressType = (int)AddressType.internalRegisters;
               tag5.name = "电压";
               tag5.dataType = DataType.Double;
               tag5.baseAddress = 7;
               tag5.bit = 0;
               tag5.parserConfig();
               config.tags.Add(tag5);

               ModbusTcpTag tag6 = new ModbusTcpTag();
               tag6.addressType = (int)AddressType.internalRegisters;
               tag6.name = "电机开关";
               tag6.dataType = DataType.Bool;
               tag6.baseAddress = 61;
               tag6.bit = 4;
               tag6.parserConfig();
               config.tags.Add(tag6);

               ModbusTcpTag tag7 = new ModbusTcpTag();
               tag7.addressType = (int)AddressType.inputCoils;
               tag7.name = "电磁开关";
               tag7.dataType = DataType.Bool;
               tag7.baseAddress = 1;
               tag7.bit = 0;
               tag7.parserConfig();
               config.tags.Add(tag7);

               var mqttConfig = new MQTTConfig();

               var fileConfig = new Config
               {
                   modbus = config,
                   mqtt = mqttConfig
               };

               String payload = JsonSerializer.Serialize(fileConfig);

               Console.WriteLine(payload);
               try
               {
                   using(StreamWriter sw = new StreamWriter("Modbus.conf"))
                   {
                       sw.Write(payload);
                   }
               }catch(Exception e)
               {

               }

*/