using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RosReestrConverter
{
	internal static class Program
	{
		private const string LogFileName = "rosreestr.log";
		private const string PostProcessFileName = "rosreestr_post.txt";
		private const string TagsFileName = "rosreestr_tags.txt";
		private const string UniqueFileNameTemplate = "{0}_({1}){2}";

		private static readonly string InvalidChars = new(
			Path.GetInvalidPathChars()
				.Concat(Path.GetInvalidFileNameChars())
				.Distinct()
				.ToArray());

		private static readonly Regex CleanerRegex = new($"[{Regex.Escape(InvalidChars)}]", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

		private static TextWriter consoleOut;

		private static string CombineAppPath(string path) =>
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

		private static void InitLog()
		{
			consoleOut = Console.Out;

			var writer = new StreamWriter(File.Create(CombineAppPath(LogFileName)))
			{
				AutoFlush = true,
			};

			Console.SetOut(writer);
		}

		private static void Log(string message, params object[] parameters)
		{
			var text = string.Format(message, parameters);
			consoleOut.WriteLine(text);
			Console.WriteLine("{0}\t{1}", DateTime.Now, text);
		}

		private static string GetNameFromXML(HashSet<string> tags, string file, ZipArchiveEntry entry)
		{
			Log("Получение имени: {0}", entry.Name);

			using var stream = entry.Open();
			var document = XDocument.Load(stream);

			var name = tags
				.Select(t => document.XPathSelectElement(t)?.Value)
				.Where(v => !string.IsNullOrWhiteSpace(v))
				.SingleOrDefault();

			if (string.IsNullOrWhiteSpace(name))
			{
				Log("Не найден тэг с именем");
				name = Path.GetFileName(file) + "_" + entry.Name;
			}

			return CleanerRegex.Replace(name, string.Empty);
		}

		private static string EnsureUniqueFileName(string filename)
		{
			var initial = filename;
			var index = 1;
			while (File.Exists(filename))
			{
				var name = string.Format(UniqueFileNameTemplate, Path.GetFileNameWithoutExtension(initial), index, Path.GetExtension(initial));
				filename = Path.Combine(Path.GetDirectoryName(initial), name);
				++index;
			}

			return filename;
		}

		private static HashSet<string> LoadTags() =>
			File.ReadAllLines(CombineAppPath(TagsFileName))
				.ToHashSet();

		private static HashSet<string> LoadPostProcess()
		{
			if (File.Exists(CombineAppPath(PostProcessFileName)))
			{
				return File.ReadAllLines(CombineAppPath(PostProcessFileName))
					.ToHashSet();
			}

			return Enumerable.Empty<string>().ToHashSet();
		}

		private static string PostProcess(HashSet<string> postProcessors, string filename)
		{
			Log("Пост обработка");
			foreach (var post in postProcessors)
			{
				filename = filename.Replace(post, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
			}

			return filename;
		}

		static Program()
		{
			InitLog();
		}

		private static void Main(string[] args)
		{
			var files = args
				.Where(f => Directory.Exists(f))
				.SelectMany(f => Directory.EnumerateFiles(f, "*.zip", SearchOption.AllDirectories))
				.Concat(args
					.Where(f => File.Exists(f))
					.Where(f => string.Equals(Path.GetExtension(f), ".zip", StringComparison.OrdinalIgnoreCase)));

			var tags = LoadTags();
			Log("Тэгов: {0}", tags.Count);

			var postProcessors = LoadPostProcess();
			Log("Записей пост. обработки: {0}", postProcessors.Count);

			Log("Всего файлов: {0}", files.Count());
			foreach (var file in files)
			{
				Log("Обработка: {0}", Path.GetFileNameWithoutExtension(file));

				using var archive = ZipFile.OpenRead(file);

				var xmlEntry = archive.Entries
					.Single(e => string.Equals(Path.GetExtension(e.Name), ".xml", StringComparison.OrdinalIgnoreCase));

				var pdfEntry = archive.Entries
					.Single(e => string.Equals(Path.GetExtension(e.Name), ".pdf", StringComparison.OrdinalIgnoreCase));

				var pdfName = GetNameFromXML(tags, file, xmlEntry);
				var output = EnsureUniqueFileName(PostProcess(postProcessors, pdfName) + ".pdf");

				Log("Итоговое имя файла: {0}", output);
				pdfEntry.ExtractToFile(output, true);
			}
		}
	}
}
