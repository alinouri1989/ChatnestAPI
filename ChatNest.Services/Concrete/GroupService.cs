using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;

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

        public async Task<Dictionary<string, GroupProfile>> CreateGroupAsync(string userId, CreateGroup dto)
        {
            var group = new Group
            {
                Name = dto.Name,
                Description = dto.Description ?? string.Empty,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow,
                Participants = new Dictionary<string, GroupParticipant>
                {
                    { userId, GroupParticipant.Admin }
                }
            };

            // Handle group photo upload
            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                byte[] photoBytes;

                var base64Data = dto.Photo.Contains(',') ? dto.Photo.Split(',')[1] : dto.Photo;
                photoBytes = Convert.FromBase64String(base64Data);

                using var photoStream = new MemoryStream(photoBytes);
                var photoUrl = await _cloudRepository.UploadPhotoAsync(
                    $"group_{group.Id}",
                    "groups",
                    "group,photo",
                    photoStream);
                group.Photo = photoUrl;
            }

            // Add selected participants
            if (dto.SelectedParticipants != null)
            {
                foreach (var participantId in dto.SelectedParticipants)
                {
                    if (participantId != userId) // Don't add creator twice
                    {
                        group.Participants[participantId] = GroupParticipant.Member;
                    }
                }
            }

            await _groupRepository.CreateOrUpdateGroupAsync(group);

            var result = new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), _mapper.Map<GroupProfile>(group) }
            };

            return result;
        }

        public async Task<Dictionary<string, GroupProfile>> EditGroupAsync(string userId, string groupId, CreateGroup dto)
        {
            var group = await _groupRepository.GetGroupByIdAsync(Guid.Parse(groupId));
            if (group == null)
                throw new NotFoundException("Group not found");

            // Check if user is admin or creator
            if (group.CreatedBy != userId &&
                (!group.Participants.ContainsKey(userId) || group.Participants[userId] != GroupParticipant.Admin))
                throw new BadRequestException("You don't have permission to edit this group");

            group.Name = dto.Name;
            group.Description = dto.Description ?? string.Empty;

            // Handle group photo upload
            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                byte[] photoBytes;

                var base64Data = dto.Photo.Contains(',') ? dto.Photo.Split(',')[1] : dto.Photo;
                photoBytes = Convert.FromBase64String(base64Data);

                using var photoStream = new MemoryStream(photoBytes);
                var photoUrl = await _cloudRepository.UploadPhotoAsync(
                    $"group_{group.Id}",
                    "groups",
                    "group,photo",
                    photoStream);
                group.Photo = photoUrl;
            }

            await _groupRepository.CreateOrUpdateGroupAsync(group);

            var result = new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), _mapper.Map<GroupProfile>(group) }
            };

            return result;
        }

        public async Task<Dictionary<string, GroupProfile>> GetGroupProfilesAsync(List<string> userGroupIds)
        {
            var result = new Dictionary<string, GroupProfile>();

            foreach (var groupId in userGroupIds)
            {
                var group = await _groupRepository.GetGroupByIdAsync(Guid.Parse(groupId));
                if (group != null)
                {
                    result.Add(groupId, _mapper.Map<GroupProfile>(group));
                }
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
            var group = await _groupRepository.GetGroupByIdAsync(Guid.Parse(groupId));
            if (group == null || !group.Participants.ContainsKey(userId))
                throw new NotFoundException("Group not found or you're not a member");

            // If user is the creator, they can't leave unless they transfer ownership or delete the group
            if (group.CreatedBy == userId)
                throw new BadRequestException("Group creator cannot leave. Transfer ownership or delete the group instead.");

            // Mark user as former member
            group.Participants[userId] = GroupParticipant.Former;
            await _groupRepository.UpdateGroupParticipantsAsync(Guid.Parse(groupId), group.Participants);

            var result = new Dictionary<string, GroupProfile>
            {
                { group.Id.ToString(), _mapper.Map<GroupProfile>(group) }
            };

            return result;
        }
    }
}