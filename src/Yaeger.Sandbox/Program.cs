// See https://aka.ms/new-console-template for more information
using Yaeger.Engine;
using Yaeger.Sandbox;

Console.WriteLine("Sandbox");

Application.Instance.Title = "Sandbox";
Application.Instance.AddScene(new GameScene());
Application.Instance.Run();
