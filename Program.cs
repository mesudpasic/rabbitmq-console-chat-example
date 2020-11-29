using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json;
using System.IO;

namespace RabbitMQChat
{
    class Program
    {
        //name of the user
        private static String Nickname { get; set; }
        //configuration interface
        private static IConfiguration config;
        // instance of rabbitMQ class that holds info on rabbitMQ server
        private static RabbitMQConfig rabbitConfig;
        static void Main(string[] args)
        {
            //create builder for configuration file 
            var builder = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json")
                 .Build();

            // Then get your values using this approach
            rabbitConfig = builder.GetSection("RabbitMQConfig").Get<RabbitMQConfig>();

            Console.WriteLine("************Example for RabbitMQ Chat using publish/subscribe************");
            Console.WriteLine("Please enter your name:");
            Nickname = Console.ReadLine();
            Console.WriteLine($"Hello {Nickname}, now you can send messages to others. To exit chat simpy type 'exit' and hit enter. ");            
            //setup factory for rabbitMQ
            var factory = new ConnectionFactory() { HostName = rabbitConfig.Host };
            //setup connection for rabbitMQ
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            //setup channel for specified queue
            channel.QueueDeclare(queue: rabbitConfig.Queue,
                                    durable: true, //the queue will survive a broker restart
                                    exclusive: false,// if true, used by only one connection and the queue will be deleted when that connection closes
                                    autoDelete: false, // if true, queue that has had at least one consumer is deleted when last consumer unsubscribes
                                    arguments: null);

            //declare consumer and it's event for receiving 
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                try
                {
                    // try parsing the message back to it's object instance, and show only message from other user
                    MessageBody message = JsonConvert.DeserializeObject<MessageBody>(Encoding.UTF8.GetString(body));
                    if (!message.Name.Equals(Nickname))
                    {
                        Console.WriteLine($"{message.Created.ToString("dd-MM-yyyy HH:mm")} {message.Name}: {message.Message}");
                    }
                }
                catch { }
                    
            };
            //sets the queue for channel
            channel.BasicConsume(queue: rabbitConfig.Queue,
                                    autoAck: true,
                                    consumer: consumer);                
            
            //read message and loop for reading and sending messages
            String msg = Console.ReadLine();
            while (!msg.Equals("exit"))
            {
                Console.WriteLine($"{DateTime.Now.ToString("dd-MM-yyyy HH:mm")} {Nickname}:{msg}");
                MessageBody m = new MessageBody();
                m.Name = Nickname;
                m.Created = DateTime.Now;
                m.Message = msg;
                SendMessage(m);
                msg = Console.ReadLine();
            }
            //close connection and app will also close, when user types in 'exit' and hits enter
            connection.Close();            

        }
        //method for sending messages, serializes the object to string and sends it over the channel
        public static void SendMessage(MessageBody message)
        {
            var factory = new ConnectionFactory() { HostName = rabbitConfig.Host };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: rabbitConfig.Queue,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
                //turn object to JSON string
                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                //send message to queue
                channel.BasicPublish(exchange: rabbitConfig.Exchange,
                                     routingKey: rabbitConfig.RoutingKey,
                                     basicProperties: null,
                                     body: body);                
            }
        }
    }
    /* class that represents configuration for rabbitMQ server info in appsettings.json */
    [JsonObject("rabbitMQ")]
    public class RabbitMQConfig
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("queue")]
        public string Queue { get; set; }
        
        [JsonProperty("exchange")]
        public string Exchange { get; set; }

        [JsonProperty("routingKey")]
        public string RoutingKey { get; set; }
    }
    /* message body class */
    [Serializable]
    public class MessageBody
    {
        public String Name { get; set; }
        public String Message { get; set; }
        public DateTime Created { get; set; }

    }

}
