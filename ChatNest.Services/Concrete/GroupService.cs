using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using System.Text.Json;

namespace ChatNest.Services.Concrete
{
    public sealed class GroupService : IGroupService
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMediaStorageRepository _mediaStorageRepository;
        private readonly IMapper _mapper;

        public GroupService(
            IGroupRepository groupRepository,
            IChatRepository chatRepository,
            IUserRepository userRepository,
            IMediaStorageRepository mediaStorageRepository,
            IMapper mapper)
        {
            _groupRepository = groupRepository;
            _chatRepository = chatRepository;
            _userRepository = userRepository;
            _mediaStorageRepository = mediaStorageRepository;
            _mapper = mapper;
        }

        private static Guid ParseRequiredGuid(string value, string parameterName)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException($"Invalid {parameterName}");
        }

        private static Dictionary<string, GroupParticipant> BuildParticipantsFromRequest(
            CreateGroup dto,
            string creatorUserId,
            Dictionary<string, GroupParticipant>? fallbackParticipants = null)
        {
            var participants = fallbackParticipants != null
                ? new Dictionary<string, GroupParticipant>(fallbackParticipants)
                : new Dictionary<string, GroupParticipant>();

            if (!string.IsNullOrWhiteSpace(dto.Participants))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, GroupParticipant>>(dto.Participants)
                                 ?? new Dictionary<string, GroupParticipant>();

                    participants = parsed
                        .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                        .ToDictionary(p => p.Key, p => p.Value);
                }
                catch (JsonException)
                {
                    throw new BadRequestException("Participants payload is invalid.");
                }
            }
            else if (dto.SelectedParticipants != null)
            {
                participants = dto.SelectedParticipants
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .ToDictionary(p => p, _ => GroupParticipant.Member);
            }

            participants.Remove(string.Empty);
            participants[creatorUserId] = GroupParticipant.Admin;

            return participants;
        }

        private async Task<GroupProfile> MapGroupToProfileSafeAsync(
            Group group,
            IReadOnlyDictionary<string, User>? usersById = null)
        {
            // ✅ این Map دیگه Participants رو دست نمی‌زنه (تو MappingProfile Ignore کردی)
            var profile = _mapper.Map<GroupProfile>(group);

            // ✅ حالا در حافظه participants رو بساز
            var dict = new Dictionary<string, ParticipantProfile>();

            foreach (var (participantId, role) in group.Participants)
            {
                User? user = null;
                if (usersById != null && usersById.TryGetValue(participantId, out var batchUser))
                {
                    user = batchUser;
                }
                else
                {
                    user = await _userRepository.GetUserByIdAsync(participantId);
                }

                dict[participantId] = new ParticipantProfile
                {
                    UserId = participantId,
                    DisplayName = user?.DisplayName ?? string.Empty,

                    // ✅ required member باید همینجا ست بشه
                    ProfilePhoto = user?.ProfilePhoto?.ToString() ?? "",

                    Role = role
                };
            }

            // ✅ حالا چون Participants set; شده، این خط کامپایل میشه
            profile.Participants = dict;

            return profile;
        }

        public async Task<Dictionary<string, GroupProfile>> CreateGroupAsync(string userId, CreateGroup dto)
        {
            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description ?? string.Empty,
                CreatedBy = userId,
                Kind = dto.Kind,
                CreatedDate = DateTime.UtcNow,
            };

            var participants = BuildParticipantsFromRequest(dto, userId);

            group.ParticipantsJson = JsonSerializer.Serialize(participants);

            // photo upload
            if (!string.IsNullOrWhiteSpace(dto.Photo))
            {
                var base64Data = dto.Photo.Contains(',') ? dto.Photo.Split(',')[1] : dto.Photo;
                var photoBytes = Convert.FromBase64String(base64Data);

                using var photoStream = new MemoryStream(photoBytes);
                var photoUrl = await _mediaStorageRepository.UploadPhotoAsync(
                    $"group_{group.Id}",
                    "groups",
                    "group,photo",
                    photoStream);

                group.Photo = photoUrl;
            }

            await _groupRepository.CreateOrUpdateGroupAsync(group);

            var profile = await MapGroupToProfileSafeAsync(group);

            return new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), profile }
            };
        }

        public async Task<Dictionary<string, GroupProfile>> EditGroupAsync(string userId, string groupId, CreateGroup dto)
        {
            var parsedGroupId = ParseRequiredGuid(groupId, "group id");
            var group = await _groupRepository.GetGroupByIdAsync(parsedGroupId);
            if (group == null)
                throw new NotFoundException("Group not found");

            if (group.CreatedBy != userId &&
                (!group.Participants.ContainsKey(userId) || group.Participants[userId] != GroupParticipant.Admin))
                throw new BadRequestException("You don't have permission to edit this group");

            group.Name = dto.Name;
            group.Description = dto.Description ?? string.Empty;
            group.Kind = dto.Kind;

            var updatedParticipants = BuildParticipantsFromRequest(dto, group.CreatedBy, group.Participants);
            group.ParticipantsJson = JsonSerializer.Serialize(updatedParticipants);

            if (!string.IsNullOrWhiteSpace(dto.Photo))
            {
                var base64Data = dto.Photo.Contains(',') ? dto.Photo.Split(',')[1] : dto.Photo;
                var photoBytes = Convert.FromBase64String(base64Data);

                using var photoStream = new MemoryStream(photoBytes);
                var photoUrl = await _mediaStorageRepository.UploadPhotoAsync(
                    $"group_{group.Id}",
                    "groups",
                    "group,photo",
                    photoStream);

                group.Photo = photoUrl;
            }
            else
            {
                group.Photo = null;
            }

            await _groupRepository.CreateOrUpdateGroupAsync(group);
            await SyncGroupChatParticipantsAsync(group.Id, updatedParticipants);

            var profile = await MapGroupToProfileSafeAsync(group);

            return new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), profile }
            };
        }

        private async Task SyncGroupChatParticipantsAsync(Guid groupId, Dictionary<string, GroupParticipant> updatedParticipants)
        {
            var chat = await _chatRepository.GetChatByIdAsync(groupId);
            if (chat == null)
                return;

            var activeParticipants = updatedParticipants
                .Where(p => p.Value != GroupParticipant.Former)
                .Select(p => p.Key)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet();

            var currentChatParticipants = await _chatRepository.GetChatParticipantsAsync(groupId);
            var currentSet = currentChatParticipants
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet();

            var toAdd = activeParticipants.Except(currentSet).ToList();
            var toRemove = currentSet.Except(activeParticipants).ToList();

            foreach (var participantId in toAdd)
            {
                await _chatRepository.AddParticipantAsync(groupId, participantId);
            }

            foreach (var participantId in toRemove)
            {
                await _chatRepository.RemoveParticipantAsync(groupId, participantId);
            }
        }

        public async Task<Dictionary<string, GroupProfile>> GetGroupProfilesAsync(List<string> userGroupIds)
        {
            var result = new Dictionary<string, GroupProfile>();
            var parsedGroupIds = userGroupIds
                .Select(id => Guid.TryParse(id, out var gid) ? gid : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (parsedGroupIds.Count == 0)
            {
                return result;
            }

            var groupsById = await _groupRepository.GetGroupsByIdsAsync(parsedGroupIds);
            var participantIds = groupsById.Values
                .SelectMany(g => g.Participants.Keys)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var usersById = await _userRepository.GetUsersByIdsAsync(participantIds);

            foreach (var groupId in userGroupIds.Distinct())
            {
                if (!Guid.TryParse(groupId, out var gid))
                {
                    continue;
                }
                if (!groupsById.TryGetValue(gid, out var group))
                {
                    continue;
                }

                result[groupId] = await MapGroupToProfileSafeAsync(group, usersById);
            }

            return result;
        }

        public async Task<List<string>> GetGroupParticipantsAsync(string userId, string groupId)
        {
            var parsedGroupId = ParseRequiredGuid(groupId, "group id");
            var group = await _groupRepository.GetGroupByIdAsync(parsedGroupId);
            if (group == null || !group.Participants.ContainsKey(userId))
                throw new NotFoundException("Group not found or access denied");

            return await _groupRepository.GetGroupParticipantsIdsAsync(parsedGroupId);
        }

        public async Task<Dictionary<string, GroupProfile>> LeaveGroupAsync(string userId, string groupId)
        {
            var gid = ParseRequiredGuid(groupId, "group id");
            var group = await _groupRepository.GetGroupByIdAsync(gid);

            if (group == null || !group.Participants.ContainsKey(userId))
                throw new NotFoundException("Group not found or you're not a member");

            var participants = group.Participants;
            participants[userId] = GroupParticipant.Former;
            var isCreatorLeaving = group.CreatedBy == userId;

            var remainingActiveParticipants = participants
                .Where(p => p.Key != userId && p.Value != GroupParticipant.Former)
                .ToList();

            if (isCreatorLeaving)
            {
                if (!remainingActiveParticipants.Any())
                {
                    group.ParticipantsJson = JsonSerializer.Serialize(participants);

                    await _chatRepository.RemoveParticipantAsync(gid, userId);
                    await _chatRepository.DeleteChatAsync(gid);
                    await _groupRepository.DeleteGroupAsync(gid);

                    var deletedGroupProfile = await MapGroupToProfileSafeAsync(group);
                    return new Dictionary<string, GroupProfile>
                    {
                        { group.Id.ToString(), deletedGroupProfile }
                    };
                }

                var nextOwner = remainingActiveParticipants
                    .FirstOrDefault(p => p.Value == GroupParticipant.Admin).Key
                    ?? remainingActiveParticipants.First().Key;

                participants[nextOwner] = GroupParticipant.Admin;
                group.CreatedBy = nextOwner;
            }

            group.ParticipantsJson = JsonSerializer.Serialize(participants);

            await _groupRepository.CreateOrUpdateGroupAsync(group);
            await _chatRepository.RemoveParticipantAsync(gid, userId);

            var profile = await MapGroupToProfileSafeAsync(group);

            return new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), profile }
            };
        }
    }
}
