using System;
using Microsoft.Azure.Devices;
using System.Threading.Tasks;
using System.Text;
using System.Linq;

namespace raspberrypi.net.core.backend
{
    class Program
    {
        private static ServiceClient _serviceClient;
        private const string _deviceId = "rpiofficeroom";
        private const string methodName = "TurnOnLight";
        private const string _deviceConnectionString = "HostName=apressiothub.azure-devices.net;SharedAccessKeyName=serviceRegistryRead;SharedAccessKey=RrBCz7SF7Zubwev0l6ZTg6OxuVMQj3CQnxB46PaEYqY=";
        private static RegistryManager _registryManager;
        static async Task Main(string[] args)
        {
            _serviceClient = ServiceClient.CreateFromConnectionString(_deviceConnectionString);
            _registryManager = RegistryManager.CreateFromConnectionString(_deviceConnectionString);
            await SendCloudToDeviceMessageAsync();
            // await ReceiveDeliveryFeedback(); receive cloud message feedbck
            await UpdateTwin();
            await InvokeDirectMethod(methodName);
            Console.WriteLine("Hello World!");
        }

        private static async Task ReceiveDeliveryFeedback()
        {
            var feedbackReceiver = _serviceClient.GetFeedbackReceiver();
            while (true)
            {
                var feedback = await feedbackReceiver.ReceiveAsync();
                if (feedback == null) continue;
                Console.WriteLine($"The feedback status is: {string.Join(",", feedback.Records.Select(s => s.StatusCode))}");
                await feedbackReceiver.CompleteAsync(feedback);
            }
        }

        private static async Task SendCloudToDeviceMessageAsync()
        {
            var message = new Message(Encoding.ASCII.GetBytes("This is a message from cloud"));
            message.Ack = DeliveryAcknowledgement.Full; // This is to request the feedback
            await _serviceClient.SendAsync(_deviceId, message);
        }

        private static async Task UpdateTwin()
        {
            var twin = await _registryManager.GetTwinAsync(_deviceId);
            var toUpdate = @"{
                tags:{
                    location: {
                        region: 'DE'
                    }
                },
                properties: {
                    desired: {
                        telemetryConfig: {
                            sendFrequency: '5m'
                        },
                        $metadata: {
                            $lastUpdated: '2020-07-14T10:47:29.8590777Z'
                        },
                        $version: 1
                    }
                }                
             }";
            await _registryManager.UpdateTwinAsync(_deviceId, toUpdate, twin.ETag);
        }

        private static async Task InvokeDirectMethod(string methodName)
        {
            var invocation = new CloudToDeviceMethod(methodName)
            {
                ResponseTimeout = TimeSpan.FromSeconds(45)
            };
            invocation.SetPayloadJson("5");
            var response = await _serviceClient.InvokeDeviceMethodAsync(_deviceId, invocation);
            Console.WriteLine(response.GetPayloadAsJson());
        }

    }
}
