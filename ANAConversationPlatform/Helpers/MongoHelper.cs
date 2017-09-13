﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using ANAConversationPlatform.Models;
using ANAConversationPlatform.Models.Sections;
using ANAConversationPlatform.Models.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using ANAConversationPlatform.Models.Activity;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static ANAConversationPlatform.Helpers.Constants;

namespace ANAConversationPlatform.Helpers
{
	public static class MongoHelper
	{
		static MongoHelper()
		{
			#region Initialize Mongo Driver
			ConventionRegistry.Register(nameof(IgnoreExtraElementsConvention), new ConventionPack { new IgnoreExtraElementsConvention(true) }, t => true);
			ConventionRegistry.Register(nameof(EnumRepresentationConvention), new ConventionPack { new EnumRepresentationConvention(BsonType.String) }, t => true);
			if (!BsonClassMap.IsClassMapRegistered(typeof(GifSection)))
				BsonClassMap.RegisterClassMap<GifSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(TextSection)))
				BsonClassMap.RegisterClassMap<TextSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(ImageSection)))
				BsonClassMap.RegisterClassMap<ImageSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(AudioSection)))
				BsonClassMap.RegisterClassMap<AudioSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(VideoSection)))
				BsonClassMap.RegisterClassMap<VideoSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(EmbeddedHtmlSection)))
				BsonClassMap.RegisterClassMap<EmbeddedHtmlSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(CarouselSection)))
				BsonClassMap.RegisterClassMap<CarouselSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(SectionContent)))
				BsonClassMap.RegisterClassMap<SectionContent>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(TextSectionContent)))
				BsonClassMap.RegisterClassMap<TextSectionContent>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(TitleCaptionSectionContent)))
				BsonClassMap.RegisterClassMap<TitleCaptionSectionContent>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(ButtonContent)))
				BsonClassMap.RegisterClassMap<ButtonContent>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(PrintOTPSection)))
				BsonClassMap.RegisterClassMap<PrintOTPSection>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(CarouselButtonContent)))
				BsonClassMap.RegisterClassMap<CarouselButtonContent>();

			if (!BsonClassMap.IsClassMapRegistered(typeof(CarouselItemContent)))
				BsonClassMap.RegisterClassMap<CarouselItemContent>();
			#endregion
		}
		public static ILogger Logger { get; set; }
		public static DatabaseConnectionSettings Settings { get; set; }

		private static MongoClient _chatClient;
		private static IMongoDatabase _chatDB;
		private static IMongoDatabase ChatDB
		{
			get
			{
				if (Settings == null)
					throw new Exception("MongoHelper.Settings is null");

				if (_chatClient == null || _chatClient == null)
				{
					_chatClient = new MongoClient(Settings.ConnectionString);
					_chatDB = _chatClient.GetDatabase(Settings.DatabaseName);
				}
				return _chatDB;
			}
		}

		public static async Task InsertActivityEventAsync(ChatActivityEvent activityEvent)
		{
			try
			{
				if (activityEvent != null && string.IsNullOrWhiteSpace(activityEvent._id))
					activityEvent._id = ObjectId.GenerateNewId().ToString();

				var coll = ChatDB.GetCollection<ChatActivityEvent>(Settings.ActivityEventLogCollectionName);
				await coll.InsertOneAsync(activityEvent);
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "InsertActivityEvent: {0}", ex.Message);
			}
		}

		#region Project Operations
		public static async Task<List<ANAProject>> GetProjectsAsync()
		{
			try
			{
				return await ChatDB.GetCollection<ANAProject>(Settings.ProjectsCollectionName).Find(new BsonDocument()).ToListAsync();
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "GetProjectsAsync: {0}", ex.Message);
				return null;
			}
		}

		public static async Task<ANAProject> GetProjectAsync(string projectId)
		{
			try
			{
				return await ChatDB.GetCollection<ANAProject>(Settings.ProjectsCollectionName).Find(x => x._id == projectId).SingleOrDefaultAsync();
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "GetProjectAsync: {0}", ex.Message);
				return null;
			}
		}

		public static async Task<ANAProject> GetProjectByNameAsync(string projectName)
		{
			try
			{
				return await ChatDB.GetCollection<ANAProject>(Settings.ProjectsCollectionName).Find(x => x.Name == projectName).SingleOrDefaultAsync();
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "GetProjectAsync: {0}", ex.Message);
				return null;
			}
		}

		public static List<ANAProject> SaveProjects(List<ANAProject> projects)
		{
			try
			{
				var coll = ChatDB.GetCollection<ANAProject>(Settings.ProjectsCollectionName);

				projects = projects.Where(x => x != null).ToList();

				Parallel.ForEach(projects, async proj =>
				{
					try
					{
						if (string.IsNullOrWhiteSpace(proj._id) || await coll.CountAsync(x => x._id == proj._id, new CountOptions { Limit = 1 }) <= 0)
						{
							if (string.IsNullOrWhiteSpace(proj._id))
								proj._id = ObjectId.GenerateNewId().ToString();
							proj.CreatedOn = proj.UpdatedOn = DateTime.UtcNow;
							await coll.InsertOneAsync(proj);
							await SaveChatFlowAsync(new ChatFlowPack
							{
								ChatContent = new List<BaseContent>(),
								ChatNodes = new List<ChatNode>(),
								CreatedOn = DateTime.UtcNow,
								UpdatedOn = DateTime.UtcNow,
								NodeLocations = new Dictionary<string, LayoutPoint>(),
								ProjectId = proj._id,
								_id = ObjectId.GenerateNewId().ToString()
							});
						}
						else
						{
							proj.UpdatedOn = DateTime.UtcNow;
							await coll.ReplaceOneAsync(x => x._id == proj._id, proj);
						}
					}
					catch (Exception ex)
					{
						Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "SaveProjectsAsync Single: {0}", ex.Message);
					}
				});
				return projects;
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "SaveProject: {0}", ex.Message);
			}
			return null;
		}
		#endregion

		#region Chat Flow Operations
		public static async Task<bool> SaveChatFlowAsync(ChatFlowPack chatFlow)
		{
			try
			{
				var chatsColl = ChatDB.GetCollection<ChatFlowPack>(Settings.ChatFlowPacksCollectionName);
				if (string.IsNullOrWhiteSpace(chatFlow.ProjectId))
					chatFlow.ProjectId = ObjectId.GenerateNewId().ToString();

				if (chatFlow.ChatContent != null)
					foreach (var content in chatFlow.ChatContent)
					{
						if (string.IsNullOrWhiteSpace(content._id))
							content._id = ObjectId.GenerateNewId().ToString();
					}

				var existingFlow = await chatsColl.Find(x => x.ProjectId == chatFlow.ProjectId).FirstOrDefaultAsync();
				if (existingFlow != null)
				{
					#region Existing Chat Flow
					if (chatFlow.ChatContent != null)
					{
						if (existingFlow.ChatContent != null)//If old content is not null, COMPARE
						{
							foreach (var content in chatFlow.ChatContent)
							{
								var existingContent = existingFlow.ChatContent.FirstOrDefault(x => x._id == content._id);
								if (existingContent != null) //Means Old content exists, its just updated
								{
									content.UpdatedOn = DateTime.UtcNow;
									content.CreatedOn = existingContent.CreatedOn;
								}
								else
								{
									content.UpdatedOn = DateTime.UtcNow;
									content.CreatedOn = DateTime.UtcNow;
								}
							}
						}
						else
						{
							foreach (var content in chatFlow.ChatContent)
								content.UpdatedOn = content.CreatedOn = DateTime.UtcNow;
						}
					}
					chatFlow.UpdatedOn = DateTime.UtcNow;
					chatFlow.CreatedOn = existingFlow.CreatedOn;

					try
					{
						if (!string.IsNullOrWhiteSpace(Settings.ChatFlowPacksBackupCollectionName))
						{
							var chatBackupColl = ChatDB.GetCollection<ChatFlowPack>(Settings.ChatFlowPacksBackupCollectionName);
							existingFlow._id = ObjectId.GenerateNewId().ToString();
							await chatBackupColl.InsertOneAsync(existingFlow);
							if ((await chatBackupColl.CountAsync(x => x.ProjectId == existingFlow.ProjectId)) > Settings.MaxBackups)
							{
								var oldestPackId = await chatBackupColl.Find(FilterDefinition<ChatFlowPack>.Empty).SortBy(x => x.UpdatedOn).Project(x => new { x._id }).FirstOrDefaultAsync();
								if (oldestPackId != null)
									await chatBackupColl.DeleteOneAsync(x => x._id == oldestPackId._id);
							}
						}
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "Unable to backup");
					}
					if ((chatFlow.WebNodeLocations == null || chatFlow.WebNodeLocations.Count <= 0) && (existingFlow.WebNodeLocations != null && existingFlow.WebNodeLocations.Count > 0))
						chatFlow.WebNodeLocations = existingFlow.WebNodeLocations;

					if (chatFlow.NodeLocations == null && existingFlow.NodeLocations != null)
						chatFlow.NodeLocations = existingFlow.NodeLocations;

					await chatsColl.ReplaceOneAsync(x => x.ProjectId == chatFlow.ProjectId, chatFlow);
					return true;
					#endregion
				}

				#region New Chat Flow
				chatFlow.CreatedOn = chatFlow.UpdatedOn = DateTime.UtcNow;
				if (chatFlow.ChatContent != null)
					foreach (var content in chatFlow.ChatContent)
						content.UpdatedOn = content.CreatedOn = DateTime.UtcNow;

				await chatsColl.InsertOneAsync(chatFlow);
				#endregion
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "SaveChatFlowAsync: {0}", ex.Message);
			}
			return false;
		}

		public static async Task<ChatFlowPack> GetChatFlowPackAsync(string projectId)
		{
			try
			{
				var pack = await ChatDB.GetCollection<ChatFlowPack>(Settings.ChatFlowPacksCollectionName).Find(x => x.ProjectId == projectId).SingleOrDefaultAsync();

				if ((pack.NodeLocations != null && pack.NodeLocations.Count > 0) && (pack.WebNodeLocations == null || pack.WebNodeLocations.Count <= 0))
					pack.WebNodeLocations = pack.NodeLocations;
				return pack;
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "GetChatFlowPackAsync: {0}", ex.Message);
				return null;
			}
		}

		public static async Task<ChatFlowPack> GetChatFlowPackByProjectNameAsync(string projectName)
		{
			try
			{
				var project = await GetProjectByNameAsync(projectName);
				if (project == null || string.IsNullOrWhiteSpace(project.Name)) return null;

				return await ChatDB.GetCollection<ChatFlowPack>(Settings.ChatFlowPacksCollectionName).Find(x => x.ProjectId == project._id).SingleOrDefaultAsync();
			}
			catch (Exception ex)
			{
				Logger.LogError(new EventId((int)LoggerEventId.MONGO_HELPER_ERROR), ex, "GetChatFlowPackByProjectNameAsync: {0}", ex.Message);
				return null;
			}
		}
		#endregion
	}
}