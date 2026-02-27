using Microsoft.Extensions.DependencyInjection;

namespace ResultPattern.Extension.MVC;

public static class DependencyInjection
{
	public static IServiceCollection  AddResponseHelper(this IServiceCollection services)
	{
		services.AddScoped<ResponseHelper>();
		services.AddHttpContextAccessor();
		return services;
	}
}