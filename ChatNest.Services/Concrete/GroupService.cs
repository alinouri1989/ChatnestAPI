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

        private async Task<GroupProfile> MapGroupToProfileSafeAsync(Group group)
        {
            // ✅ این Map دیگه Participants رو دست نمی‌زنه (تو MappingProfile Ignore کردی)
            var profile = _mapper.Map<GroupProfile>(group);

            // ✅ حالا در حافظه participants رو بساز
            var dict = new Dictionary<string, ParticipantProfile>();

            foreach (var (participantId, role) in group.Participants)
            {
                // ⚠️ این متد رو مطابق IUserRepository خودت تنظیم کن
                var user = await _userRepository.GetUserByIdAsync(participantId);

                dict[participantId] = new ParticipantProfile
                {
                    UserId = participantId,
                    DisplayName = user?.DisplayName ?? string.Empty,

                    // ✅ required member باید همینجا ست بشه
                    ProfilePhoto = user?.ProfilePhoto?.ToString() ?? "/Image/DefaultUserProfilePhoto.png",

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
            var group = await _groupRepository.GetGroupByIdAsync(Guid.Parse(groupId));
            if (group == null)
                throw new NotFoundException("Group not found");

            if (group.CreatedBy != userId &&
                (!group.Participants.ContainsKey(userId) || group.Participants[userId] != GroupParticipant.Admin))
                throw new BadRequestException("You don't have permission to edit this group");

            group.Name = dto.Name;
            group.Description = dto.Description ?? string.Empty;
            group.ParticipantsJson = JsonSerializer.Serialize(
                BuildParticipantsFromRequest(dto, group.CreatedBy, group.Participants));

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

        public async Task<Dictionary<string, GroupProfile>> GetGroupProfilesAsync(List<string> userGroupIds)
        {
            var result = new Dictionary<string, GroupProfile>();

            foreach (var groupId in userGroupIds)
            {
                if (!Guid.TryParse(groupId, out var gid)) continue;

                var group = await _groupRepository.GetGroupByIdAsync(gid);
                if (group == null) continue;

                result[groupId] = await MapGroupToProfileSafeAsync(group);
            }

            return result;
        }

        public async Task<List<string>> GetGroupParticipantsAsync(string userId, string groupId)
        {
            var group = await _groupRepository.GetGroupByIdAsync(Guid.Parse(groupId));
            if (group == null || !group.Participants.ContainsKey(userId))
                throw new NotFoundException("Group not found or access denied");

            return await _groupRepository.GetGroupParticipantsIdsAsync(Guid.Parse(groupId));
        }

        public async Task<Dictionary<string, GroupProfile>> LeaveGroupAsync(string userId, string groupId)
        {
            var gid = Guid.Parse(groupId);
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
