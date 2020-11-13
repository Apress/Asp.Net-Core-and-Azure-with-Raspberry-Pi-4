using System;
using System.Text;
using Iot.Device.CpuTemperature;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using raspberrypi.net.core.Models;
using Microsoft.Azure.Devices.Shared;

namespace raspberrypi.net.core
{
    class Program
    {
        private static CpuTemperature _rpiCpuTemp = new CpuTemperature();
        private const string _deviceConnectionString = "HostName=apressiothub.azure-devices.net;DeviceId=rpiofficeroom;SharedAccessKey=Zz4OyJO6odR5aLu6x9tzSpE8sUy3vBEfQThsRipN2WA=";
        private static int _messageId = 0;
        private static DeviceClient _deviceClient;
        private const double _temperatureThreshold = 40;
        public const string DeviceId = "rpiofficeroom";
        private const string methodName = "TurnOnLight";

        static async Task Main(string[] args)
        {
            _deviceClient = DeviceClient.CreateFromConnectionString(_deviceConnectionString, TransportType.Mqtt);            
            // Create a handler for the direct method call
            _deviceClient.SetMethodHandlerAsync(methodName, TurnOnLight, null).Wait();
            // Set desired property update callback
            await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChangedAsync, null).ConfigureAwait(false);

            var twin = await _deviceClient.GetTwinAsync();
            Console.WriteLine($"Initial Twin: {twin.ToJson()}");
            TwinCollection reportedProperties, telemetryConfig;
            reportedProperties = new TwinCollection();
            telemetryConfig = new TwinCollection();
            telemetryConfig["sendFrequency"] = "5m";
            reportedProperties["telemetryConfig"] = telemetryConfig;

            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
            Console.WriteLine("Waiting 30 seconds for IoT Hub Twin updates...");
            await Task.Delay(3 * 1000);

            while (true)
            {
                if (_rpiCpuTemp.IsAvailable)
                {
                    await SendToIoTHub(_rpiCpuTemp.Temperature.Celsius);
                    Console.WriteLine("The device data has been sent");
                }
                Thread.Sleep(5000); // Sleep for 5 seconds
            }
            // await ReceiveCloudToDeviceMessageAsync(); // Cloud to device receiver 
        }

        private static async Task ReceiveCloudToDeviceMessageAsync()
        {
            while (true)
            {
                var cloudMessage = await _deviceClient.ReceiveAsync();
                if (cloudMessage == null) continue;
                Console.WriteLine($"The received message is: {Encoding.ASCII.GetString(cloudMessage.GetBytes())}");
                await _deviceClient.CompleteAsync(cloudMessage); // Send feedback
            }
        }

        private static async Task OnDesiredPropertyChangedAsync(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"New desired property is {desiredProperties.ToJson()}");
            TwinCollection reportedProperties, telemetryConfig;
            reportedProperties = new TwinCollection();
            telemetryConfig = new TwinCollection();
            telemetryConfig["status"] = "success";
            reportedProperties["telemetryConfig"] = telemetryConfig;
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
        }

        private static Task<MethodResponse> TurnOnLight(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Here is the call from cloud to turn of the light!");
            var result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        private static async Task SendToIoTHub(double tempCelsius)
        {
            string jsonData = JsonConvert.SerializeObject(new DeviceData()
            {
                MessageId = _messageId++,
                Temperature = tempCelsius
            });
            var messageToSend = new Message(Encoding.UTF8.GetBytes(jsonData));
            messageToSend.Properties.Add("TemperatureAlert", (tempCelsius > _temperatureThreshold) ? "true" : "false");
            await _deviceClient.SendEventAsync(messageToSend).ConfigureAwait(false);
        }
    }
}