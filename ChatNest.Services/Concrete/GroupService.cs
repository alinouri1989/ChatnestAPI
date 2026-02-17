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
        private readonly IUserRepository _userRepository;
        private readonly ICloudRepository _cloudRepository;
        private readonly IMapper _mapper;

        public GroupService(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            ICloudRepository cloudRepository,
            IMapper mapper)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _cloudRepository = cloudRepository;
            _mapper = mapper;
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

            var participants = new Dictionary<string, GroupParticipant>
            {
                { userId, GroupParticipant.Admin }
            };

            if (dto.SelectedParticipants != null)
            {
                foreach (var participantId in dto.SelectedParticipants)
                {
                    if (!string.IsNullOrWhiteSpace(participantId) && participantId != userId)
                        participants[participantId] = GroupParticipant.Member;
                }
            }

            group.ParticipantsJson = JsonSerializer.Serialize(participants);

            // photo upload
            if (!string.IsNullOrWhiteSpace(dto.Photo))
            {
                var base64Data = dto.Photo.Contains(',') ? dto.Photo.Split(',')[1] : dto.Photo;
                var photoBytes = Convert.FromBase64String(base64Data);

                using var photoStream = new MemoryStream(photoBytes);
                var photoUrl = await _cloudRepository.UploadPhotoAsync(
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

            if (!string.IsNullOrWhiteSpace(dto.Photo))
            {
                var base64Data = dto.Photo.Contains(',') ? dto.Photo.Split(',')[1] : dto.Photo;
                var photoBytes = Convert.FromBase64String(base64Data);

                using var photoStream = new MemoryStream(photoBytes);
                var photoUrl = await _cloudRepository.UploadPhotoAsync(
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

            if (group.CreatedBy == userId)
                throw new BadRequestException("Group creator cannot leave. Transfer ownership or delete the group instead.");

            var participants = group.Participants;
            participants[userId] = GroupParticipant.Former;

            group.ParticipantsJson = JsonSerializer.Serialize(participants);

            await _groupRepository.UpdateGroupParticipantsAsync(gid, participants);

            var profile = await MapGroupToProfileSafeAsync(group);

            return new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), profile }
            };
        }
    }
}
