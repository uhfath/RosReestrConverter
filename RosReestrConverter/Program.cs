using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RosReestrConverter
{
	internal static class Program
	{
		private static readonly string InvalidChars = new(
			Path.GetInvalidPathChars()
				.Concat(Path.GetInvalidFileNameChars())
				.Distinct()
				.ToArray());

		private static readonly Regex CleanerRegex = new($"[{Regex.Escape(InvalidChars)}]", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

		private static readonly IEnumerable<string> Tags = new[]
		{
			"/extract_base_params_room/room_record/address_room/address/address/readable_address",
			"/extract_base_params_land/land_record/address_location/address/readable_address",
			"/extract_base_params_build/build_record/address_location/address/readable_address",
		};

		private static string GetNameFromXML(string file, ZipArchiveEntry entry)
		{
			Console.WriteLine("Получение имени: {0}", entry.Name);

			using var stream = entry.Open();
			var document = XDocument.Load(stream);

			var name = Tags
				.Select(t => document.XPathSelectElement(t)?.Value)
				.Where(v => !string.IsNullOrWhiteSpace(v))
				.SingleOrDefault();

			if (string.IsNullOrWhiteSpace(name))
			{
				Console.Error.WriteLine("Не найден тэг с именем");
				name = Path.GetFileName(file) + "_" + entry.Name;
			}

			return CleanerRegex.Replace(name, string.Empty);
		}

		private static void Main(string[] args)
		{
			var files = args
				.Where(f => string.Equals(Path.GetExtension(f), ".zip", StringComparison.OrdinalIgnoreCase));

			Console.WriteLine("Всего файлов: {0}", files.Count());

			foreach (var file in files)
			{
				Console.WriteLine("Обработка: {0}", Path.GetFileNameWithoutExtension(file));

				using var archive = ZipFile.OpenRead(file);

				var xmlEntry = archive.Entries
					.Single(e => string.Equals(Path.GetExtension(e.Name), ".xml", StringComparison.OrdinalIgnoreCase));

				var pdfName = GetNameFromXML(file, xmlEntry);

				Console.WriteLine("Итоговое имя файла: {0}", pdfName);

				var pdfEntry = archive.Entries
					.Single(e => string.Equals(Path.GetExtension(e.Name), ".pdf", StringComparison.OrdinalIgnoreCase));

				pdfEntry.ExtractToFile(Path.ChangeExtension(pdfName, ".pdf"), true);
			}
		}
	}
}
