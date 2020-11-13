using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Iot.Device.CpuTemperature;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace raspberrypi.net.core.central
{
    public class Program
    {
        private static string idScope = Environment.GetEnvironmentVariable("ID_SCOPE");
        private static string centralDeviceId = Environment.GetEnvironmentVariable("CENTRAL_DEVICE_ID");
        private static string primaryKey = Environment.GetEnvironmentVariable("PRIMARY_KEY");
        private const string endPoint = "global.azure-devices-provisioning.net";
        private static CpuTemperature _temperature = new CpuTemperature();
        private static TwinCollection reportedProperties = new TwinCollection();
        private static int _messageId = 0;
        private static DeviceClient _deviceClient;
        static async Task Main(string[] args)
        {
            try
            {
                using var security = new SecurityProviderSymmetricKey(centralDeviceId, primaryKey, null);
                var deviceRegistrationResult = await RegisterDeviceAsync(security);
                if (deviceRegistrationResult.Status != ProvisioningRegistrationStatusType.Assigned) return;
                var auth = new DeviceAuthenticationWithRegistrySymmetricKey(deviceRegistrationResult.DeviceId,
                (security as SecurityProviderSymmetricKey).GetPrimaryKey());
                _deviceClient = DeviceClient.Create(deviceRegistrationResult.AssignedHub, auth, TransportType.Mqtt);
                _deviceClient.SetDesiredPropertyUpdateCallbackAsync(HandleSettingChanged, null).GetAwaiter().GetResult();
                _deviceClient.SetMethodHandlerAsync("TakeThePicture", CommandTakeThePicture, null).Wait();

                await SendMessage(_temperature.Temperature.Celsius);

            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Hm, that's an error: {ex}");
            }
        }

        private static Task<MethodResponse> CommandTakeThePicture(MethodRequest methodRequest, object userContext)
        {
            // Get the data from the payload
            var payload = Encoding.UTF8.GetString(methodRequest.Data);
            Console.WriteLine(payload);
            // Code to take the picture
            // Save in the given format
            // Return the image URL
            // Imagine that your device is setup with a camera
            //Acknowledge the direct method call
            string result = "{\"result\": \"Executed : " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        private static async Task SendMessage(double temperature)
        {
            while (true)
            {
                if (_temperature.IsAvailable)
                {
                    var dataToSend = new Telemetry() { MessageId = ++_messageId, Temperature = temperature };
                    var stringToSend = JsonConvert.SerializeObject(dataToSend);
                    var messageToSend = new Message(Encoding.UTF8.GetBytes(stringToSend));
                    await _deviceClient.SendEventAsync(messageToSend).ConfigureAwait(false);
                }
                Thread.Sleep(3000);
            }
        }

        private static async Task<DeviceRegistrationResult> RegisterDeviceAsync(SecurityProviderSymmetricKey security)
        {
            using var transportHandler = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly);
            var provDeviceClient = ProvisioningDeviceClient.Create(endPoint, idScope, security, transportHandler);
            return await provDeviceClient.RegisterAsync();
        }

        static async Task HandleSettingChanged(TwinCollection desiredProperties, object userContext)
        {
            var setting = "Room";
            if (desiredProperties.Contains(setting))
            {
                var roomChange = reportedProperties[setting] = desiredProperties[setting];
            }
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }
    }

    class Telemetry
    {
        [JsonPropertyAttribute(PropertyName = "Temperature")]
        public double Temperature { get; set; } = 0;
        [JsonPropertyAttribute(PropertyName = "MessageId")]
        public int MessageId { get; set; } = 0;
    }
}
