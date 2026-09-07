using System.Text.Json;

string links = "https://ms-adf.azure.com/monitoring/pipelineruns?factory=%2Fsubscriptions%2FA9722D60-AB16-472B-ADA8-DDA89B11C422%2FresourceGroups%2F8512-data-prod%2Fproviders%2FMicrosoft.DataFactory%2Ffactories%2FDPlat-Prod-DataFactoryTwo&runGroupId=a6ae5a20-de8c-4f88-a4e4-04d90e348d1c";

string queryParametersJson = AzureMonitoringLinkService.DeconstructLink(links);
string constructedLink = AzureMonitoringLinkService.BuildLink(
	"factory",
	"pipeline-run-id",
	"A9722D60-AB16-472B-ADA8-DDA89B11C422",
	"8512-data-prod",
	"Microsoft.DataFactory",
	"DPlat-Prod-DataFactoryTwo",
	"a6ae5a20-de8c-4f88-a4e4-04d90e348d1c");

Console.WriteLine(queryParametersJson);
Console.WriteLine(constructedLink);

public static class AzureMonitoringLinkService
{
	public static string BuildLink(
		string serviceType,
		string runId,
		string subscriptionId,
		string resourceGroup,
		string providers,
		string serviceName,
		string groupId,
		string? baseLink = null)
	{
		(string serviceParameter, string resourceType, string defaultBaseLink) = serviceType.ToLowerInvariant() switch
		{
			"factory" => ("factory", "factories", "https://ms-adf.azure.com/en/monitoring/pipelineruns/"),
			"workspace" => ("workspace", "workspaces", "https://ms.web.azuresynapse.net/en/monitoring/pipelineruns/"),
			_ => throw new ArgumentException(
				"Service type must be either 'factory' or 'workspace'.",
				nameof(serviceType))
		};

		string normalizedBaseLink = string.IsNullOrWhiteSpace(baseLink)
			? defaultBaseLink
			: $"{baseLink.TrimEnd('/')}/";

		return $"{normalizedBaseLink}{Uri.EscapeDataString(runId)}" +
			$"?{serviceParameter}=%2Fsubscriptions%2F{Uri.EscapeDataString(subscriptionId)}" +
			$"%2FresourceGroups%2F{Uri.EscapeDataString(resourceGroup)}" +
			$"%2Fproviders%2F{Uri.EscapeDataString(providers)}" +
			$"%2F{resourceType}%2F{Uri.EscapeDataString(serviceName)}" +
			$"&runGroupId={Uri.EscapeDataString(groupId)}";
	}

	public static string DeconstructLink(string link)
	{
		string[] urlParts = link.Split('?', 2);
		var queryParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var missingKeys = new List<string>();

		if (urlParts.Length > 1)
		{
			foreach (string parameter in urlParts[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
			{
				string[] parameterParts = parameter.Split('=', 2);
				string key = Uri.UnescapeDataString(parameterParts[0]);
				string value = parameterParts.Length > 1 ? parameterParts[1] : string.Empty;

				if (key.Equals("factory", StringComparison.OrdinalIgnoreCase) ||
					key.Equals("workspace", StringComparison.OrdinalIgnoreCase))
				{
					queryParameters["serviceType"] = key.ToLowerInvariant();
					string[] resourceParts = value.Split(
						["%20", "%3A", "%2F"],
						StringSplitOptions.RemoveEmptyEntries);

					for (int index = 0; index < resourceParts.Length; index += 2)
					{
						string resourceKey = Uri.UnescapeDataString(resourceParts[index]);
						if (index + 1 >= resourceParts.Length || string.IsNullOrWhiteSpace(resourceParts[index + 1]))
						{
							queryParameters.Remove(resourceKey);
							missingKeys.Add(resourceKey);
							continue;
						}

						queryParameters[resourceKey] = Uri.UnescapeDataString(resourceParts[index + 1]);
					}
				}
				else if (string.IsNullOrWhiteSpace(value))
				{
					queryParameters.Remove(key);
					missingKeys.Add(key);
				}
				else
				{
					queryParameters[key] = Uri.UnescapeDataString(value);
				}
			}
		}

		try
		{
			if (missingKeys.Count > 0)
			{
				throw new InvalidOperationException(
					$"Missing value for key(s): {string.Join(", ", missingKeys)}");
			}
		}
		catch (InvalidOperationException exception)
		{
			Console.Error.WriteLine(exception.Message);
		}

		return JsonSerializer.Serialize(queryParameters);
	}
}

