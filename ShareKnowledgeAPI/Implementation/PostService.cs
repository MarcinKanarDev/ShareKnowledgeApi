﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShareKnowledgeAPI.Authorization;
using ShareKnowledgeAPI.Database;
using ShareKnowledgeAPI.Entities;
using ShareKnowledgeAPI.Exceptions;
using ShareKnowledgeAPI.Mapper.DTOs;
using ShareKnowledgeAPI.Seeder;
using ShareKnowledgeAPI.Services;
using System.Security.Claims;

namespace ShareKnowledgeAPI.Implementation
{
    public class PostService : IPostService
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context;
        private readonly IUserContextService _userContextService;
        private readonly DataSeeder _dataSeeder;

        public PostService(ApplicationDbContext dbContext, IMapper mapper, DataSeeder dataSeeder,
            IAuthorizationService authorizationService, IUserContextService contextService) 
        {
            _authorizationService = authorizationService;
            _mapper = mapper;
            _dataSeeder = dataSeeder;
            _context = dbContext;
            _userContextService = contextService;
        }

        public async Task<IEnumerable<PostDto>> GetAllPostsAsync(string searchPhrase)
        {
            _dataSeeder.SeedData();

            var posts = await _context.Posts
                .Include(p => p.Comments)
                .Include(p => p.Categories)
                .Where(p => searchPhrase == null || (p.Title.ToLower().Contains(searchPhrase.ToLower()) 
                        || p.Description.ToLower().Contains(searchPhrase.ToLower())))
                .ToListAsync();

            var postDtos = _mapper.Map<IEnumerable<PostDto>>(posts);

            return postDtos;
        }

        public async Task<PostDto> GetPostByIdAsync(int postId)
        {
            var post = await _context.Posts.
                FirstOrDefaultAsync(p => p.Id == postId);

            if (post is null)
                throw new NotFoundException($"Post not found");

            var postDto = _mapper.Map<PostDto>(post);

            return postDto;
        }

        public async Task<int> CreatePostAsync(CreatePostDto postDto)
        {
            var categories = _mapper.Map<List<Category>>(postDto.CategoryDtos);
                       
            var post = _mapper.Map<Post>(postDto);
            post.Categories = categories;
            post.CreatedById = _userContextService.GetUserId;
           
            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();

            return post.Id;
        }

        public async Task DeletePostAsync(int postId)
        {
            var post = await _context.Posts.
                FirstOrDefaultAsync(p => p.Id == postId);

            if (post is null)
                throw new NotFoundException($"Post not found");

            var authorizeResult = _authorizationService.AuthorizeAsync(_userContextService.User, post,
                new ResourceOperationRequirement(ResourceOperation.Delete)).Result;

            if (!authorizeResult.Succeeded)
            {
                throw new ForbidException("You don't have an access to this resorce.");
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
        }
      
        public async Task UpdatePostAsync(Post post)
        {
            var postFromDb = await _context.Posts
                .FirstOrDefaultAsync(p => p.Id == post.Id);

            if (postFromDb is null)
                throw new NotFoundException($"Post not found");

            var authorizeResult = _authorizationService.AuthorizeAsync(_userContextService.User, postFromDb,
                new ResourceOperationRequirement(ResourceOperation.Update)).Result;

            if (!authorizeResult.Succeeded) 
            {
                throw new ForbidException("You don't have an access to this resorce.");
            }

            postFromDb.Title = post.Title;
            postFromDb.Description = post.Description;
            postFromDb.Brains = post.Brains;
            postFromDb.Comments = post.Comments;
            postFromDb.Categories = post.Categories;
            
            await _context.SaveChangesAsync();
        }
    }
}
