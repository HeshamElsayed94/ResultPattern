using Microsoft.Extensions.DependencyInjection;

namespace ResultPattern.Extension.MinimalAPI;

public static class DependencyInjection
{
	public static IServiceCollection  AddResponseHelper(this IServiceCollection services)
	{
		services.AddSingleton<ResponseHelper>();
		return services;
	}
}