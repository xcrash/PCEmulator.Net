using System.ServiceModel;

namespace PCEmulator.Js.Wrapper
{
	[ServiceContract]
	public interface IJsHostHandler
	{
		[OperationContract]
		void DebugLog(string log);

		[OperationContract]
		void Ping();
	}
}